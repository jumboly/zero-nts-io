# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build everything (Release is the working default; net10.0)
dotnet build -c Release

# All tests (should be 207/207 passing — any regression is a real bug)
dotnet test tests/FastNtsWk.Tests -c Release

# One test class
dotnet test tests/FastNtsWk.Tests -c Release --filter "FullyQualifiedName~FastV4Tests"

# One test case
dotnet test tests/FastNtsWk.Tests -c Release --filter "FullyQualifiedName~EdgeCaseTests.Nested_GeometryCollection"

# Benchmarks (short job; --filter '*' takes ~15 min on M-series)
dotnet run -c Release --project bench/FastNtsWk.Benchmarks -- --filter '*WkbRead*' --job short
dotnet run -c Release --project bench/FastNtsWk.Benchmarks -- --filter '*WktRead*' --job short
```

## Architecture

The library ships four parallel implementations of WKT/WKB I/O that all produce `NetTopologySuite.Geometries.Geometry` — it is a **drop-in replacement for NTS's readers/writers**, not an independent geometry model. Any API design that touches the reader/writer surface must keep NTS types at the boundary (see `memory/project_nts_drop_in.md` for the reasoning).

### The V1→V4 staircase

`src/FastNtsWk.Fast/` holds the speed-optimized code as four discrete classes per reader (not flags on a single class). Each step adds exactly one optimization so that the **per-step diff in a BenchmarkDotNet run equals that technique's isolated contribution**:

- **V1**: `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` input, no string intermediates, stock `double.TryParse`.
- **V2** (WKT only): adds `Internal/FastDoubleParser.cs` — ASCII fast path for `[+-]?\d+(\.\d+)?([eE][+-]?\d+)?`, mantissa as `ulong` times an exact `Pow10[]` entry (exponent ≤ ±22 and mantissa ≤ 2^53), otherwise falls back to `double.TryParse`.
- **V3** (WKT only): adds `Internal/PooledCoordinateBuffer` — `ArrayPool<Coordinate>` / `ArrayPool<LinearRing>` for growable scratch buffers. Does **not** apply to the final coordinate array (see "ArrayPool scope" below).
- **V4**: adds `Internal/CoordinateBlockReader.cs` (LE = `MemoryMarshal.Cast`-style reinterpret, BE = `Vector128.Shuffle` byte-swap) and `Internal/PackedSequenceBuilder.cs` (hands a freshly-allocated `double[]` to `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` with ownership transferred, zero copy).

WKB only ships V1 and V4 — there is no text to parse, so the V2/V3 optimizations don't apply. Writers ship only one variant (V4-equivalent) because the per-stage gains for writing don't show up in benchmarks.

### ArrayPool scope

`PackedCoordinateSequenceFactory.Create(double[], dim, measures)` takes ownership of the array and holds it for the geometry's lifetime, so **the final coordinate array cannot come from `ArrayPool`**. V3 pools only scratch (ring arrays, coordinate growth buffers); V4 still allocates the final `double[]` from the heap but avoids the `Coordinate` struct intermediate entirely.

### Why these specific SIMD / unsafe choices

`Vector128.Shuffle` is used (not `Avx2.Shuffle`) so the SIMD byte-swap path works on Apple Silicon (ARM `tbl`) as well as x86 (`pshufb`). For WKB-LE there is nothing for SIMD to do — the byte layout is already the target `PackedDoubleCoordinateSequence` layout, so V4 is a single `memcpy`. SIMD only earns its name in the BE benchmark (`WkbReadBenchmarks` with `ByteOrder.BigEndian`).

### NtsGeometryServices requirement

All readers require services constructed with `PackedCoordinateSequenceFactory.DoubleFactory`. The default `NtsGeometryServices.Instance` uses `CoordinateArraySequenceFactory`, which silently drops Z/M on packed round-trips. `src/FastNtsWk.Reference/NtsServicesFactory.cs` builds the correct instance; tests and benchmarks share it via `Samples.Services` and `FixtureSource.Services`.

### Test oracle

Correctness is validated by comparing against NTS's own reader/writer output with **bit-for-bit coordinate equality** (`BitConverter.DoubleToInt64Bits`, via `tests/FastNtsWk.Tests/Fixtures/CoordinateAsserts.cs`). V2/V3/V4 WKT tests allow 1-ULP drift because `FastDoubleParser`'s fast path rounds independently of the BCL parser; WKB tests always require 0 ULP. Never canonicalize polygon ring orientation in tests — doing so hides real bugs.

## Scope

- **OGC ISO WKB only.** EWKB (PostGIS SRID/Z/M high-bit flags, mask `0xE0000000`) is rejected with `FormatException` by design; see `FastWkbReaderV1.Read` / `FastWkbReaderV4.Read`.
- No spatial operations (intersects, buffer, etc.). I/O only — delegate those to NTS.
- `POINT EMPTY` in WKB is encoded as all-NaN ordinates (OGC spec). NTS emits and consumes this; our readers and writers match.
