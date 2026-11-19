"""Windows failure-path regression; mocks are not actual Windows validation."""
import ast
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
ROOT = Path(__file__).resolve().parents[1]
HERE = ROOT / 'Tools/DdaLab'
sys.path.insert(0, str(HERE))
import runtime

class Startup(unittest.TestCase):
    def sdk_tree(self, root):
        sdk = root / 'sdk/8.0.428'; sdk.mkdir(parents=True)
        (sdk / 'Microsoft.NETCoreSdk.BundledVersions.props').write_text('<Project><KnownFrameworkReference Include="Microsoft.NETCore.App" TargetFramework="net8.0" TargetingPackVersion="8.0.34" /></Project>', encoding='utf-8')
        ref = root / 'packs/Microsoft.NETCore.App.Ref/8.0.34/ref/net8.0/System.Runtime.dll'
        ref.parent.mkdir(parents=True); ref.touch()
        return ref

    def test_coexisting_sdk_selects_eight(self):
        with tempfile.TemporaryDirectory(prefix='한국어 SDK ') as tmp:
            root = Path(tmp); exe = root / 'dotnet.exe'; exe.touch(); self.sdk_tree(root)
            result = subprocess.CompletedProcess([], 0, f'10.0.402 [{root / "sdk"}]\n8.0.428 [{root / "sdk"}]\n', '')
            with patch.object(runtime, 'run', return_value=result):
                sdk = runtime.select_sdk(str(exe))
            self.assertEqual(sdk['version'], '8.0.428')
            runtime.pin_sdk(root, sdk)
            self.assertEqual(json.loads((root/'global.json').read_text(encoding='utf-8'))['sdk'], dict(version='8.0.428',rollForward='disable',allowPrerelease=False))

    def test_missing_reference_pack_no_download(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);exe=root/'dotnet';exe.touch();self.sdk_tree(root).unlink()
            with patch.object(runtime,'run',return_value=subprocess.CompletedProcess([],0,f'8.0.428 [{root/"sdk"}]','')):
                with self.assertRaisesRegex(RuntimeError,'오프라인 참조 팩 누락'):
                    runtime.select_sdk(str(exe))

    def test_ten_only_is_not_eight(self):
        with tempfile.TemporaryDirectory() as tmp:
            exe=Path(tmp)/'dotnet';exe.touch()
            with patch.object(runtime,'run',return_value=subprocess.CompletedProcess([],0,'10.0.402 [C:/sdk]','')):
                with self.assertRaisesRegex(RuntimeError,'.NET 8 SDK가 없습니다'):
                    runtime.select_sdk(str(exe))

    def test_winerror2_names_command(self):
        with patch.object(runtime.subprocess,'run',side_effect=FileNotFoundError(2,'[WinError 2]')):
            with self.assertRaisesRegex(RuntimeError,'missing-dotnet.*build'):
                runtime.run(['missing-dotnet','build'],cwd='test-build')

    def test_git_missing_is_warning(self):
        with patch.object(runtime.subprocess,'run',side_effect=FileNotFoundError(2,'[WinError 2]')):
            sha,warning=runtime.git_revision(ROOT)
            self.assertIsNone(sha);self.assertIn('git',warning);self.assertIn('rev-parse',warning)

    def test_git_non_repository_is_warning(self):
        with patch.object(runtime,'run',return_value=subprocess.CompletedProcess([],128,'','not a git repository')):
            self.assertIn('exit=128',runtime.git_revision(ROOT)[1])

    def test_all_text_io_explicit_utf8(self):
        for name in ('lab.py','runtime.py','launch.py'):
            tree=ast.parse((HERE/name).read_text(encoding='utf-8'))
            for node in ast.walk(tree):
                if isinstance(node,ast.Call) and isinstance(node.func,ast.Attribute) and node.func.attr in ('read_text','write_text','open'):
                    encoding=next((k.value for k in node.keywords if k.arg=='encoding'),None)
                    self.assertIsNotNone(encoding,f'{name}:{node.lineno} relies on Windows CP949')
                    self.assertIn(ast.literal_eval(encoding),('utf-8','utf-8-sig'))

    def test_cmd_does_not_require_powershell_or_git(self):
        cmd=(HERE/'Start-DdaLab.cmd').read_text(encoding='utf-8')
        self.assertIn('-X utf8',cmd);self.assertIn('%~dp0launch.py',cmd)
        self.assertNotIn('powershell.exe',cmd.lower());self.assertNotIn('git ',cmd.lower())

    def test_missing_file_diagnostic(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(RuntimeError,'필수 파일 누락'):
                runtime.check_files(Path(tmp),Path(tmp),[])

if __name__=='__main__':unittest.main()
