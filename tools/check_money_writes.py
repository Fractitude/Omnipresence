#!/usr/bin/env python3
"""Guard: cash and crypto move only through Transactions (docs/transactions.md).

Scans the game's own code for anything that changes a cash or crypto balance outside the files that are allowed to.
Run it from anywhere before committing a change that touches money:

    python tools/check_money_writes.py

Exit code 0 = clean, 1 = a violation (each is printed with file and line).
"""
import os
import re
import sys

ROOT = os.environ.get("MONEY_GUARD_ROOT") or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAN = ["Code", "Editor", "Addons"]

# The only files allowed to change a balance: the transaction system, the wallet's named exempt path, and the record.
ALLOWED = {
    "Code/Wallet/TransactionRules.cs",
    "Code/Wallet/Transactions.cs",
    "Code/Wallet/PlayerWallet.cs",
    "Code/Progression/ProgressRecord.cs",
}

# Files that hold their own fields named Money/Crypto that are not a balance (the stats log's per-player totals).
IGNORED_PREFIXES = ("Code/Stats/",)

RULES = [
    # The old mutators are gone; nothing may bring them back.
    (re.compile(r"\b(AddCash|TrySpendCash|AddCrypto|TrySpendCrypto|TryPayCash|TryPayCrypto)\s*\("),
     "old wallet mutator (use Transactions)"),
    # The one low-level mutator of a record.
    (re.compile(r"\.Settle\s*\(\s*(TransactionCurrency|currency|cur)\b"),
     "ProgressRecord.Settle outside the transaction system"),
    # Writing the balance fields directly.
    (re.compile(r"\.(Money|Crypto)\s*(\+=|-=|=(?![=>]))"),
     "direct write to ProgressRecord.Money/Crypto"),
    # Object initializer of a record with a balance.
    (re.compile(r"new\s+ProgressRecord\s*\{[^}]*\b(Money|Crypto)\s*="),
     "ProgressRecord created with a balance"),
]


def main():
    bad = []
    for top in SCAN:
        for folder, _, files in os.walk(os.path.join(ROOT, top)):
            for name in files:
                if not name.endswith((".cs", ".razor")):
                    continue
                path = os.path.join(folder, name)
                rel = os.path.relpath(path, ROOT).replace("\\", "/")
                if rel in ALLOWED or rel.startswith(IGNORED_PREFIXES):
                    continue
                with open(path, encoding="utf-8", errors="replace") as f:
                    for number, line in enumerate(f, 1):
                        code = line.split("//")[0]
                        for pattern, why in RULES:
                            if pattern.search(code):
                                bad.append(f"{rel}:{number}: {why}: {line.strip()}")
    if bad:
        print("Money moved outside Transactions:")
        print("\n".join(bad))
        return 1
    print("OK: no cash or crypto written outside the transaction system.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
