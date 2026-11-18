"""Read-only scope guard: explicit immutable baseline plus exact reviewed file hashes.
Default HEAD behavior is preserved for legacy art-worktree callers.
"""
import hashlib
import json
from pathlib import Path
import subprocess


def verify_scope(root, paths, contract=None):
    root = Path(root)
    baseline, allowed = 'HEAD', {}
    if contract:
        data = json.loads(Path(contract).read_text())
        baseline = data['baseline']
        # Verify an actual commit, not a mutable branch name in a review contract.
        resolved = subprocess.check_output(['git', 'rev-parse', '--verify', baseline + '^{commit}'], cwd=root, text=True).strip()
        if resolved != baseline:
            raise ValueError('Scope contract baseline must be a full commit SHA')
        allowed = data['files']
    changed = subprocess.check_output(['git', 'diff', '--name-only', baseline, '--', *paths], cwd=root, text=True).splitlines()
    untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '--', *paths], cwd=root, text=True).splitlines()
    for name in set(changed + untracked):
        approval = allowed.get(name)
        if not approval or not approval.get('reason'):
            return False
        f = root / name
        # Only explicitly reviewed nonsensitive files are ever opened for hashing.
        if f.is_symlink() or not f.is_file() or hashlib.sha256(f.read_bytes()).hexdigest() != approval['sha256']:
            return False
    return True
