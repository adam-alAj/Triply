#!/usr/bin/env python3
"""TRIPLY — dataset manifest generator (FR-DATA-001 versioning).

Regenerates DATASET_MANIFEST.json for the curated CSVs:
SHA-256 per file, row counts, coverage totals, and the semver dataset
version. Run it after ANY change to curated-data/ and bump
--dataset-version according to the dataset's own policy:

    patch — price/description corrections, no schema/scope change
    minor — new places/destinations/categories added
    major — scope change (D5 destination list, column/schema changes)

The manifest is the machine-checkable half of the review record: a
reviewer re-runs this script and diffs DATASET_MANIFEST.json to see
exactly what changed in the dataset between versions.

Usage (from AI/01-Dataset/seed/):
    python generate_manifest.py                       # verify-only against existing manifest
    python generate_manifest.py --write               # regenerate manifest for current CSVs
    python generate_manifest.py --write --dataset-version 1.1.0
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List

SCRIPT_DIR = Path(__file__).resolve().parent
DATA_DIR = SCRIPT_DIR.parent / "curated-data"
MANIFEST_PATH = SCRIPT_DIR / "DATASET_MANIFEST.json"

DATASET_FILES = [
    "Country.csv",
    "Currency.csv",
    "Destination.csv",
    "PlaceCategory.csv",
    "CostCategory.csv",
    "InterestCategory.csv",
    "Place.csv",
    "Extra_AI_Context.csv",
]

SHA256_CHUNK = 1 << 20


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as fh:
        while True:
            chunk = fh.read(SHA256_CHUNK)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def read_rows(path: Path) -> List[Dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def build_manifest(data_dir: Path, dataset_version: str) -> Dict:
    files_block: Dict[str, Dict] = {}
    for name in DATASET_FILES:
        path = data_dir / name
        if not path.exists():
            sys.exit(f"ERROR: required dataset file missing: {path}")
        rows = read_rows(path)
        files_block[name] = {
            "sha256": sha256_file(path),
            "rows": len(rows),
        }

    countries = read_rows(data_dir / "Country.csv")
    destinations = read_rows(data_dir / "Destination.csv")
    places = read_rows(data_dir / "Place.csv")
    currencies = read_rows(data_dir / "Currency.csv")

    currency_id_to_iso = {
        r["id"].strip(): r["iso_code"].strip() for r in currencies
    }
    country_id_to_iso = {
        r["id"].strip(): r["iso_code"].strip() for r in countries
    }

    destination_names = [
        d["name"].strip() for d in destinations if d.get("name")
    ]

    price_coverage: Dict[str, Dict] = {}
    for iso in sorted(currency_id_to_iso.values()):
        currency_places = [
            p for p in places
            if currency_id_to_iso.get((p.get("currency_id") or "").strip()) == iso
        ]
        price_coverage[iso] = {
            "places": len(currency_places),
            "min_reference_price": (
                min(float(p["reference_price"]) for p in currency_places)
                if currency_places
                else None
            ),
            "max_reference_price": (
                max(float(p["reference_price"]) for p in currency_places)
                if currency_places
                else None
            ),
        }

    return {
        "dataset_version": dataset_version,
        "schema_contract_ref": "FR-DATA-001 (SRS); Database Design §6.3–§6.7, §26, §28",
        "scope_ref": "D5-agreed destinations (MVP supported list)",
        "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "files": files_block,
        "coverage": {
            "countries": len(countries),
            "destinations": len(destinations),
            "destination_names": destination_names,
            "places": len(places),
            "places_per_destination": {
                d["name"].strip(): sum(
                    1 for p in places
                    if (p.get("destination_id") or "").strip() == (d.get("id") or "").strip()
                )
                for d in destinations
            },
            "price_coverage_by_currency": price_coverage,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate the dataset manifest.")
    parser.add_argument("--data-dir", default=str(DATA_DIR), help=argparse.SUPPRESS)
    parser.add_argument(
        "--write",
        action="store_true",
        help="Write DATASET_MANIFEST.json (default: compare only).",
    )
    parser.add_argument(
        "--dataset-version",
        default=None,
        help="Semver for the dataset version stored in the manifest (required with --write for a new version).",
    )
    args = parser.parse_args()

    data_dir = Path(args.data_dir)

    existing_version = None
    if MANIFEST_PATH.exists():
        with MANIFEST_PATH.open(encoding="utf-8") as fh:
            existing_version = json.load(fh).get("dataset_version")

    version = args.dataset_version or existing_version
    if args.write and not version:
        parser.error("--dataset-version is required with --write when no manifest exists yet")
    if not version:
        version = "0.0.0-unknown"

    manifest = build_manifest(data_dir, version)

    if not args.write:
        if not MANIFEST_PATH.exists():
            print(f"No manifest found at {MANIFEST_PATH}")
            print("Run: python generate_manifest.py --write --dataset-version <semver>")
            return 1
        with MANIFEST_PATH.open(encoding="utf-8") as fh:
            existing = json.load(fh)
        if existing["files"] == manifest["files"]:
            print(
                f"Verified: curated CSVs match manifest (dataset v{existing['dataset_version']})."
            )
            return 0
        print("Dataset has CHANGED since the manifest was generated.")
        print("Review the change, bump the version, then re-run with --write --dataset-version <semver>.")
        return 1

    with MANIFEST_PATH.open("w", encoding="utf-8", newline="\n") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    print(f"Wrote {MANIFEST_PATH} (dataset v{version})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
