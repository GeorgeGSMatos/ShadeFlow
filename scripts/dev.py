#!/usr/bin/env python3
"""WSL supervisor: live WPF styles, incremental builds and cooperative Windows restarts."""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tarfile
import time
import urllib.request
import uuid

STYLE_FILES = {f"Styles/{name}.xaml" for name in ("Colors", "Controls")}
EXCLUDED = {"bin", "obj", "Checks", "Tests", "scripts", "artifacts", "runtime", ".dev", "__pycache__"}
SUFFIXES = {".cs", ".xaml", ".csproj", ".ico", ".manifest", ".props", ".targets"}


def atomic_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
    temporary.replace(path)


def read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return {}


def scan(root: Path) -> dict[str, str]:
    result = {}
    for base in (root,):
        for directory, names, files in os.walk(base):
            names[:] = sorted(name for name in names if name not in EXCLUDED)
            for name in sorted(files):
                path = Path(directory) / name
                if path.suffix not in SUFFIXES or path.name in {"main.cjs", "preload.cjs"}:
                    continue
                try:
                    result[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
                except OSError:
                    pass  # An editor may be atomically replacing the file; poll again.
    return result


def changed(before: dict, after: dict) -> set[str]:
    return {path for path in before.keys() | after.keys() if before.get(path) != after.get(path)}


def change_kind(paths: set[str]) -> str:
    return "styles" if paths and paths <= STYLE_FILES else "restart"


def windows_path(path: Path) -> str:
    return subprocess.check_output(["wslpath", "-w", str(path.resolve())], text=True).strip()


def setup_sdk(root: Path, args: argparse.Namespace) -> Path:
    candidates = [args.dotnet, os.environ.get("SHADEFLOW_DOTNET"), shutil.which("dotnet"), str(root / ".dev/sdk/dotnet"), str(Path.home() / ".dotnet/dotnet")]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            version = subprocess.run([candidate, "--version"], capture_output=True, text=True)
            if version.returncode == 0 and version.stdout.strip().startswith("8."):
                return Path(candidate).resolve()
    if args.dotnet:
        raise RuntimeError("O SDK informado não é um SDK .NET 8 válido.")
    manifest = read_json(root / "scripts/dev-sdk.json")
    tools = root / ".dev"
    tools.mkdir(exist_ok=True)
    archive = Path(args.sdk_archive).resolve() if args.sdk_archive else tools / "sdk-download.tar.gz"
    if not args.sdk_archive:
        print("Preparando o SDK local .NET 8 (somente na primeira execução)…", flush=True)
        with urllib.request.urlopen(manifest["url"], timeout=60) as response, archive.open("wb") as target:
            shutil.copyfileobj(response, target)
    with archive.open("rb") as package_file:
        digest = hashlib.file_digest(package_file, "sha512").hexdigest()
    if digest.lower() != manifest["sha512"].lower():
        raise RuntimeError("O SHA-512 do SDK não corresponde ao manifesto oficial.")
    target = tools / "sdk"
    target.mkdir(exist_ok=True)
    with tarfile.open(archive, "r:gz") as package:
        package.extractall(target, filter="data")
    return target / "dotnet"


class Supervisor:
    def __init__(self, root: Path, args: argparse.Namespace, dotnet: Path):
        self.root, self.args, self.dotnet = root, args, dotnet
        self.dev = root / ".dev"
        self.control = self.dev / "control" / uuid.uuid4().hex
        self.control.mkdir(parents=True)
        self.process: subprocess.Popen | None = None
        self.log_handle = None
        self.revision = ""
        self.stopping = False
        self.env = os.environ.copy()
        self.env.update(DOTNET_CLI_HOME=str(self.dev / "dotnet-home"), DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_GENERATE_ASPNET_CERTIFICATE="false", DOTNET_NOLOGO="1", NUGET_PACKAGES=str(Path(args.nuget_cache).resolve() if args.nuget_cache else self.dev / "nuget"))
        self.built = False

    def build(self, restore: bool = False) -> Path | None:
        print("Compilando as alterações…", flush=True)
        output = self.dev / "build"
        command = [str(self.dotnet), "build", str(self.root / "ShadeFlow.csproj"), "-c", "Debug", "-o", str(output), "-p:UseAppHost=false", "-p:AssemblyName=ShadeFlow.Dev", "--nologo", "--verbosity", "minimal"]
        if self.built and not restore:
            command.append("--no-restore")
        completed = subprocess.run(command, env=self.env)
        if completed.returncode:
            print("Alteração não aplicada. A janela anterior continua disponível; corrija o erro e salve novamente.", flush=True)
            return None
        self.built = True
        revision = str(time.time_ns())
        target = self.dev / "runs" / revision

        shutil.copytree(output, target)
        return target

    def launch(self, payload: Path) -> None:
        self.revision = payload.name
        arguments = ["/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", windows_path(self.root / "scripts/Run-ShadeFlowDev.ps1"), "-AssemblyPath", windows_path(payload / "ShadeFlow.Dev.dll"), "-SourceRoot", windows_path(self.root), "-ControlDirectory", windows_path(self.control), "-Revision", self.revision]
        if self.args.hidden:
            arguments.append("-Hidden")
        log = self.dev / "application.log"
        self.log_handle = log.open("a", encoding="utf-8")
        self.process = subprocess.Popen(arguments, stdout=self.log_handle, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            ready = read_json(self.control / "ready.json")
            if ready.get("revision") == self.revision:
                atomic_json(self.dev / "session.json", {"control": str(self.control), "revision": self.revision, "ready": ready})
                print("ShadeFlow aberto. Estilos ao vivo; código e estrutura com reinício automático. Ctrl+C para encerrar.", flush=True)
                return
            if self.process.poll() is not None:
                startup_error = self.control / "startup-error.txt"
                if startup_error.exists():
                    raise RuntimeError(startup_error.read_text(encoding="utf-8-sig"))
                raise RuntimeError("O aplicativo não iniciou. Consulte " + str(log))
            time.sleep(.2)
        raise RuntimeError("A abertura ainda não foi confirmada. Consulte " + str(log))

    def request(self, command: str) -> dict:
        identity = uuid.uuid4().hex
        atomic_json(self.control / "command.json", {"id": identity, "command": command, "revision": self.revision})
        notified = False
        started = time.monotonic()
        while self.process is not None and self.process.poll() is None:
            reply = read_json(self.control / "reply.json")
            if reply.get("id") == identity:
                return reply
            if not notified and time.monotonic() - started > 3:
                print("Aguardando concluir a tarefa atual ou fechar o editor no ShadeFlow…", flush=True)
                notified = True
            time.sleep(.2)
        reply = read_json(self.control / "reply.json")
        if reply.get("id") == identity:
            return reply
        return {"ok": False, "error": "A janela foi fechada."}

    def close(self, command: str) -> bool:
        if self.process is None or self.process.poll() is not None:
            return True
        reply = self.request(command)
        if not reply.get("ok"):
            print(reply.get("error", "Não foi possível fechar a janela."), flush=True)
            return False
        self.process.wait()
        if self.log_handle:
            self.log_handle.close()
        return True

    def run(self) -> None:
        initial = self.build(restore=True)
        if initial is None:
            raise RuntimeError("Corrija os erros acima e inicie novamente.")
        before = scan(self.root)
        self.launch(initial)
        pending: dict | None = None
        last_change = 0.0
        while not self.stopping and self.process is not None and self.process.poll() is None:
            after = scan(self.root)
            if after != (pending if pending is not None else before):
                pending, last_change = after, time.monotonic()
            if pending is not None and time.monotonic() - last_change >= .7:
                paths = changed(before, pending)
                before, pending = pending, None
                if not paths:
                    continue
                if change_kind(paths) == "styles":
                    reply = self.request("styles")
                    print("Estilos atualizados sem fechar a janela." if reply.get("ok") else "Estilos não aplicados: " + str(reply.get("error")), flush=True)
                else:
                    payload = self.build(restore=any(path.endswith((".csproj", ".props", ".targets")) for path in paths))
                    if payload is not None and not self.stopping and self.close("restart"):
                        self.launch(payload)
            time.sleep(.3)
        self.close("stop")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--dotnet")
    parser.add_argument("--nuget-cache")
    parser.add_argument("--sdk-archive")
    parser.add_argument("--prepare", action="store_true")
    parser.add_argument("--hidden", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    root = args.root.resolve()
    dev = root / ".dev"
    dev.mkdir(exist_ok=True)
    with (dev / "runner.lock").open("w") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print("O modo de desenvolvimento já está aberto.", flush=True)
            return 1
        runner = None
        try:
            dotnet = setup_sdk(root, args)
            if args.prepare:
                print("SDK pronto: " + str(dotnet), flush=True)
                return 0
            runner = Supervisor(root, args, dotnet)
            signal.signal(signal.SIGINT, lambda *_: setattr(runner, "stopping", True))
            signal.signal(signal.SIGTERM, lambda *_: setattr(runner, "stopping", True))
            runner.run()
            return 0
        except Exception as error:
            print("Não foi possível continuar: " + str(error), file=sys.stderr, flush=True)
            if runner is not None:
                runner.close("stop")
            return 1


if __name__ == "__main__":
    raise SystemExit(main())
