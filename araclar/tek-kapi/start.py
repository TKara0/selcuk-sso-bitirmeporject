"""Tek Kapı'yı başlatır ve tarayıcıda açar. Durdurmak için Ctrl+C."""
import os
import subprocess
import sys
import time
import urllib.request

ROOT = os.path.dirname(os.path.abspath(__file__))
URL = "http://127.0.0.1:4545/"


def up():
    try:
        urllib.request.urlopen(URL, timeout=1)
        return True
    except Exception:
        return False


def main():
    if up():
        # Çalışan örneğin Tek Kapı penceresini öne getir (portal kendi penceresinde açılır)
        req = urllib.request.Request(URL + "api/focus", method="POST", headers={"x-tek-kapi": "1"})
        try:
            urllib.request.urlopen(req, timeout=5)
        except Exception:
            pass
        print("Tek Kapı zaten çalışıyor:", URL)
        return

    if not os.path.isdir(os.path.join(ROOT, "node_modules")):
        print("Bağımlılıklar kuruluyor (npm install)...")
        subprocess.run("npm install", cwd=ROOT, shell=True, check=True)

    proc = subprocess.Popen("npm start", cwd=ROOT, shell=True)
    for _ in range(40):
        if proc.poll() is not None:
            sys.exit("Sunucu başlatılamadı.")
        if up():
            break
        time.sleep(0.5)
    else:
        proc.terminate()
        sys.exit("Sunucu 20 saniyede açılmadı.")

    # Sunucu portalı kendi yönettiği pencerede açar; varsayılan tarayıcıda ayrıca açmaya gerek yok
    print("Tek Kapı çalışıyor:", URL, "(durdurmak için Ctrl+C)")
    try:
        proc.wait()
    except KeyboardInterrupt:
        subprocess.run(f"taskkill /PID {proc.pid} /T /F", shell=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    main()
