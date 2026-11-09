"""Focused checks for source watching and change classification."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('shadeflow_dev', Path(__file__).with_name('dev.py'))
dev = importlib.util.module_from_spec(spec)
spec.loader.exec_module(dev)

class WatcherTests(unittest.TestCase):
    def test_styles_reload_without_restart(self):
        self.assertEqual(dev.change_kind({'Styles/Colors.xaml', 'Styles/Controls.xaml'}), 'styles')

    def test_window_structure_requires_restart(self):
        self.assertEqual(dev.change_kind({'MainWindow.xaml'}), 'restart')
        self.assertEqual(dev.change_kind({'Styles/Controls.xaml', 'Services/ThemeService.cs'}), 'restart')

    def test_detects_added_changed_and_removed_files(self):
        self.assertEqual(dev.changed({'a': 'old', 'b': 'x'}, {'a': 'new', 'c': 'x'}), {'a', 'b', 'c'})

    def test_watcher_excludes_generated_and_test_sources(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ('MainWindow.xaml', 'Services/ThemeService.cs', 'Styles/Colors.xaml', '.dev/build/App.cs', 'obj/App.cs', 'Tests/Test.cs', 'artifacts/Test.cs', 'scripts/Test.cs'):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('value')
            self.assertEqual(set(dev.scan(root)), {'MainWindow.xaml', 'Services/ThemeService.cs', 'Styles/Colors.xaml'})

    def test_manifest_and_project_properties_are_watched(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ('app.manifest', 'ShadeFlow.csproj', 'Directory.Build.props'):
                (root / name).write_text('value')
            self.assertEqual(set(dev.scan(root)), {'app.manifest', 'ShadeFlow.csproj', 'Directory.Build.props'})

    def test_atomic_control_files_roundtrip(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'session.json'
            dev.atomic_json(path, {'revision': 'abc', 'message': 'alteração'})
            self.assertEqual(dev.read_json(path)['message'], 'alteração')
            self.assertFalse(path.with_suffix('.json.tmp').exists())

if __name__ == '__main__':
    unittest.main()
