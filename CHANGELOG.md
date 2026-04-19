# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-04-19

### Added
- Initial public release.
- `ZWktReader` / `ZWkbReader` / `ZWktWriter` / `ZWkbWriter` as drop-in replacements for NetTopologySuite's WKT/WKB IO. Returns stock `NetTopologySuite.Geometries.Geometry`.
- Span-based tokenizer, custom ASCII double parser, SIMD (`Vector128.Shuffle`) byte-swap for WKB BE, and zero-copy `PackedCoordinateSequence` ownership transfer.
- 1,254 oracle tests against NetTopologySuite 2.6.0 (bit-level coordinate equality for WKB; 1 ULP tolerance for WKT due to independent double-rounding).
- Benchmarks against NTS with synthetic and real-world data (`bench/Data/A45_Kagawa_NationalForest.wkb`, 国土数値情報 CC BY 4.0).

[0.1.0]: https://github.com/jumboly/zero-nts-io/releases/tag/v0.1.0
