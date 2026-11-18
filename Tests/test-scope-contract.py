"""Prove reviewed scope never becomes an unconditional pass after a commit."""
import hashlib,json,subprocess,tempfile,unittest
from pathlib import Path
from scope_contract import verify_scope
class ScopeContractTests(unittest.TestCase):
 def test_exact_baseline_and_changed_bytes(self):
  with tempfile.TemporaryDirectory(prefix='scope-contract-') as tmp:
   r=Path(tmp)
   def git(*args):return subprocess.check_output(['git',*args],cwd=r,text=True).strip()
   git('init','-q');git('config','user.email','test@example.invalid');git('config','user.name','Scope test')
   (r/'Assets').mkdir();f=r/'Assets/approved.cs';f.write_text('baseline');git('add','.');git('commit','-qm','baseline');base=git('rev-parse','HEAD')
   f.write_text('reviewed integration');approval={'baseline':base,'files':{'Assets/approved.cs':{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'reason':'Reviewed isolated fixture'}}}
   contract=r/'contract.json';contract.write_text(json.dumps(approval))
   self.assertTrue(verify_scope(r,['Assets'],contract))
   git('add','Assets');git('commit','-qm','reviewed');self.assertTrue(verify_scope(r,['Assets'],contract))
   f.write_text('unapproved mutation');git('add','Assets');git('commit','-qm','unapproved')
   self.assertFalse(verify_scope(r,['Assets'],contract),'clean HEAD cannot hide post-review changes')
   f.write_text('reviewed integration');(r/'Assets/extra.cs').write_text('new unreviewed source')
   self.assertFalse(verify_scope(r,['Assets'],contract),'unknown untracked files remain protected')
   (r/'Assets/extra.cs').unlink();approval['baseline']='HEAD';contract.write_text(json.dumps(approval))
   with self.assertRaises(ValueError):verify_scope(r,['Assets'],contract)
if __name__=='__main__':unittest.main()
