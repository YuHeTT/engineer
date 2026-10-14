"""Cache full native geometry for alpha-mask rendering."""
from concurrent.futures import ProcessPoolExecutor,as_completed
import pickle
from pathlib import Path
from eps_geometry import inspect_eps
WORK=Path(__file__).resolve().parent/'.work'
SOURCE=Path(r'C:\Users\StarryXiaoKui\Downloads\EPS格式')
def cache(n):
    file=WORK/f'native{n:02d}.pkl'
    if file.exists():return n
    d=inspect_eps(SOURCE/f'源文件 ({n}).eps',True)
    file.write_bytes(pickle.dumps(d,protocol=pickle.HIGHEST_PROTOCOL))
    return n
if __name__=='__main__':
    with ProcessPoolExecutor(max_workers=4) as pool:
        for future in as_completed([pool.submit(cache,n) for n in range(1,33)]):
            print(f'Geometry {future.result():02d}',flush=True)
