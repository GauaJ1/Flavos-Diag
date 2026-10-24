#!/usr/bin/env python3
"""Verificador de clean room do Flavos Diag.

Uso: python -I cleanroom_scan.py <raiz-do-projeto>

Procura, em src/, tests/, docs/ e README.md:
  1. nomes de produtos de terceiros (diagnostico/benchmark) e de ferramentas de engenharia reversa;
  2. binarios (.exe, .dll, .sys) versionados fora de bin/ e obj/.
Retorna codigo 1 se houver achados. Excecoes: scripts/cleanroom_allow.txt
(um caminho relativo por linha, relativo a raiz do projeto).
"""
import os
import re
import sys

FORBIDDEN = [
    r"aida\s?64", r"cinebench", r"furmark", r"cpu-?z", r"gpu-?z", r"hwinfo",
    r"crystal\s?disk\s?info", r"\bocct\b", r"prime\s?95", r"3d\s?mark",
    r"geekbench", r"passmark",
    r"ghidra", r"\bida\s?pro\b", r"\bhopper\b", r"dnspy", r"\bilspy\b",
    r"x64dbg", r"rea-agents", r"morluto", r"decompil",
]
PATTERN = re.compile("|".join(FORBIDDEN), re.IGNORECASE)

SCAN_DIRS = ["src", "tests", "docs"]
SCAN_FILES = ["README.md"]
TEXT_EXT = {
    ".cs", ".xaml", ".resx", ".md", ".json", ".txt", ".csproj", ".props",
    ".yml", ".yaml", ".xml", ".ps1", ".py", ".sh", ".config",
}
SKIP_DIRS = {"bin", "obj", ".git", "node_modules", ".vs", ".claude", "TestResults"}
BIN_EXT = {".exe", ".dll", ".sys"}


def load_allow(root):
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.join(here, "cleanroom_allow.txt")
    allowed = set()
    if os.path.isfile(path):
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line and not line.startswith("#"):
                    allowed.add(line.replace("\\", "/"))
    return allowed


def walk(root, rel):
    base = os.path.join(root, rel)
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            yield os.path.join(dirpath, name)


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    allowed = load_allow(root)
    findings = []

    candidates = []
    for d in SCAN_DIRS:
        if os.path.isdir(os.path.join(root, d)):
            candidates.extend(walk(root, d))
    for f in SCAN_FILES:
        p = os.path.join(root, f)
        if os.path.isfile(p):
            candidates.append(p)

    for path in candidates:
        rel = os.path.relpath(path, root).replace("\\", "/")
        if rel in allowed:
            continue
        ext = os.path.splitext(path)[1].lower()
        if ext not in TEXT_EXT:
            continue
        try:
            with open(path, encoding="utf-8", errors="ignore") as fh:
                for n, line in enumerate(fh, 1):
                    m = PATTERN.search(line)
                    if m:
                        findings.append(f"{rel}:{n}: termo proibido '{m.group(0)}'")
        except OSError:
            continue

    # binarios versionados fora de bin/obj
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            if os.path.splitext(name)[1].lower() in BIN_EXT:
                rel = os.path.relpath(os.path.join(dirpath, name), root).replace("\\", "/")
                if rel not in allowed:
                    findings.append(
                        f"{rel}: binario versionado; registre em docs/LICENCAS.md e em cleanroom_allow.txt"
                    )

    if findings:
        print("CLEAN ROOM: achados que precisam de correcao:")
        for f in findings:
            print(" -", f)
        return 1
    print("CLEAN ROOM: nenhum achado.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
