"""Start a fresh Last to Die ranking epoch while retaining the run audit trail."""
from __future__ import annotations

import argparse
import os
import sqlite3
from contextlib import closing
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", required=True, type=Path, help="Existing API SQLite database")
    parser.add_argument("--backup", required=True, type=Path, help="New SQLite backup file")
    parser.add_argument("--apply", action="store_true", help="Create the backup and advance the ranking epoch")
    args = parser.parse_args()

    database = args.db.resolve(strict=True)
    backup = args.backup.resolve(strict=False)
    if database == backup or backup.exists():
        parser.error("the backup must be a new file distinct from the database")

    with closing(sqlite3.connect(database, timeout=30)) as db:
        db.row_factory = sqlite3.Row
        state = db.execute("SELECT epoch FROM last_to_die_rankings_state WHERE id=1").fetchone()
        if state is None:
            parser.error("ranking state is missing; deploy and start the updated API first")
        epoch = int(state["epoch"])
        count = db.execute(
            "SELECT COUNT(*) FROM last_to_die_runs WHERE ranking_epoch=?", (epoch,)
        ).fetchone()[0]
        print(f"Current epoch: {epoch}; visible run records: {count}")
        if not args.apply:
            print("Dry run only. Pass --apply to back up the database and reset the rankings.")
            return

        backup.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        previous_umask = os.umask(0o077)
        try:
            with closing(sqlite3.connect(backup)) as destination:
                db.backup(destination)
        finally:
            os.umask(previous_umask)
        os.chmod(backup, 0o600)
        try:
            db.execute("BEGIN IMMEDIATE")
            current = db.execute("SELECT epoch FROM last_to_die_rankings_state WHERE id=1").fetchone()
            next_epoch = int(current["epoch"]) + 1
            db.execute(
                "UPDATE last_to_die_rankings_state SET epoch=?, reset_at=strftime('%s','now') WHERE id=1",
                (next_epoch,),
            )
            db.commit()
        except Exception:
            db.rollback()
            raise
        print(f"Backup: {backup}")
        print(f"New epoch: {next_epoch}; old run records retained but no longer ranked.")


if __name__ == "__main__":
    main()
