# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## コマンド

```bash
# ビルド（Release が基本、net10.0）
dotnet build -c Release

# 全テスト（1122/1122 通過が期待値。退行は即バグ）
dotnet test tests/ZeroWkX.Tests -c Release

# 1 クラスだけ
dotnet test tests/ZeroWkX.Tests -c Release --filter "FullyQualifiedName~ZReaderTests"

# 1 ケースだけ
dotnet test tests/ZeroWkX.Tests -c Release --filter "FullyQualifiedName~EdgeCaseTests.Nested_GeometryCollection"

# ベンチ（部分実行推奨、--filter '*' は M 系で 30 分超）
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- --filter '*WkbRead*' --job short
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- --filter '*Dimensions*' --job short
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- --filter '*NaturalData*' --job short
```

実測せずスモーク確認したい時は `--job dry`。

## アーキテクチャ

本ライブラリは **NetTopologySuite の `Geometry` を入出力する WKT/WKB I/O のドロップイン置換**であり、独自のジオメトリモデルは持たない。Reader/Writer の API を触るとき、境界で NTS 型を保つ方針を崩さないこと。

公開クラスは `namespace NetTopologySuite.IO` 直下（NTS 公式 `WKTReader` / `WKBReader` と同じ階層、ケーシング差で記号衝突なし）。NuGet `PackageId` は独立した `ZeroWkX`。ディレクトリ・プロジェクト名は `ZeroWkX.*` で統一（リポジトリルート `fast-nts-wk/` だけ旧名のまま）。

### プロジェクト構成

- **`src/ZeroWkX/`** — 唯一の公開パッケージ。`ZWktReader` / `ZWkbReader` / `ZWktWriter` / `ZWkbWriter` の 4 つの sealed class を `namespace NetTopologySuite.IO` 直下に公開。インターフェース抽象は持たない（`InternalsVisibleTo` で Stages/Tests/Benchmarks に internal ヘルパを見せる）。
- **`src/ZeroWkX.Stages/`** — 段階差分ベンチ専用の非公開プロジェクト。`ZWktReaderV1` / `ZWktReaderV2` / `ZWktReaderV3` / `ZWkbReaderV1` のみを持つ（V4 は公開版 `ZeroWkX` に移された）。`ZeroWkX` を ProjectReference し、共通 internal (`WktCursor` / `FastDoubleParser`) を借用する。
- **`src/ZeroWkX.Naive/`** — `string.Split` / `BinaryReader` による素朴実装。比較ベンチとオラクル補助用の非公開プロジェクト。
- **`src/ZeroWkX.Reference/`** — NTS 公式 IO の薄いラッパー。`NtsServicesFactory.CreatePacked()` がここに住み、`PackedCoordinateSequenceFactory.DoubleFactory` を差し込んだ `NtsGeometryServices` を供給する。非公開プロジェクト。

Test / Bench は 4 プロジェクト全部に ProjectReference する。

### 段階最適化の階段

公開版 `ZWktReader` / `ZWkbReader` は下記 4 段の最適化を**重ねた最終形**。`ZeroWkX.Stages` の V1/V2/V3 + 公開版で 4 段が揃い、ベンチマークで各段の純効果を見せる（各段で最適化手法を 1 つだけ積み足す設計のため、**BenchmarkDotNet の隣り合う行の差分がその手法の純効果**になる）:

- **V1**: `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` 入力、中間文字列ゼロ、`double.TryParse` は BCL 標準のまま
- **V2**（WKT のみ）: `Internal/FastDoubleParser.cs` を追加。ASCII 限定 `[+-]?\d+(\.\d+)?([eE][+-]?\d+)?` に mantissa `ulong` × `Pow10[]` テーブル乗算。mantissa > 2^53 / |exp| > 22 のときは BCL フォールバック
- **V3**（WKT のみ）: `Internal/PooledCoordinateBuffer` を追加。`ArrayPool` はスクラッチ用のみ（詳細は後述）
- **Z**（旧 V4、公開版）: `Internal/CoordinateBlockReader.cs`（LE = `MemoryMarshal.Cast` 再解釈、BE = `Vector128.Shuffle` バイトスワップ）と `Internal/PackedSequenceBuilder.cs`（`PackedCoordinateSequenceFactory.Create(double[], dim, measures)` に所有権渡しでゼロコピー）+ `PooledDoubleBuffer`

WKB には V1 と Z しか無い。テキスト解析が無く V2/V3 の最適化が効かないため。Writer は Z 1 本（段階ごとの差がベンチで見えない）。

**命名の注記**: `FastDoubleParser` クラスの `Fast` は業界標準名 `fast_float` 由来の技術用語であり、ブランドの `Fast` とは別概念。ブランドを `Z`/`ZeroWkX` に変更しても `FastDoubleParser` の名前は保つ。

### Internal ヘルパの所在

公開版の最適化に必要なヘルパは `src/ZeroWkX/Internal/`（namespace `ZeroWkX.Internal`、NTS namespace への侵入を避けるため分離）に置く:

- `WktCursor.cs` — span ベースの WKT トークナイザ（V1-V3 と Z が共有）
- `FastDoubleParser.cs` — カスタム double パーサ（V2/V3 と Z が共有）
- `PackedSequenceBuilder.cs` — `PackedCoordinateSequenceFactory` への所有権渡し（Z のみ使用）
- `PooledDoubleBuffer.cs` — `double[]` 用 ArrayPool ラッパー（Z のみ使用）
- `CoordinateBlockReader.cs` — SIMD バイトスワップ（Z WKB Reader / Writer のみ使用）

V3 専用の `PooledCoordinateBuffer.cs` は `src/ZeroWkX.Stages/Internal/`（namespace `ZeroWkX.Stages.Internal`）に残している。`ZeroWkX.Stages` は `ZeroWkX` への ProjectReference + `InternalsVisibleTo("ZeroWkX.Stages")` で公開プロジェクトの internal ヘルパにアクセスする。

### ArrayPool の適用範囲

`PackedCoordinateSequenceFactory.Create(double[], dim, measures)` は**配列の所有権を受け取り**、ジオメトリ寿命中に保持し続ける。したがって**最終座標配列は ArrayPool から取れない**。V3 はスクラッチ（リング集計配列・座標成長バッファ）のみを pool 化。公開版 Z は最終 `double[]` をヒープ確保しつつ `Coordinate` 構造体を全く経由しない。

### SIMD / unsafe の選択理由

`Vector128.Shuffle` を使う理由は Apple Silicon（ARM `tbl`）と x86（`pshufb`）の双方にマップされるから（`Avx2.Shuffle` だと Mac でベンチが動かない）。WKB-LE は SIMD の出番なし（バイト列が既に `PackedDoubleCoordinateSequence` レイアウトなので単一 `memcpy`）。SIMD の価値は BE 系（`WkbReadBenchmarks` / `WkbReadDimensionsBenchmarks` / `NaturalDataBenchmarks` の BE パラメータ）でのみ現れる。

### NtsGeometryServices の要件

全 Reader は `PackedCoordinateSequenceFactory.DoubleFactory` で構築した services を要求する。既定の `NtsGeometryServices.Instance` は `CoordinateArraySequenceFactory` で、packed ラウンドトリップ時に Z/M を黙って落とす。正しいインスタンスは `src/ZeroWkX.Reference/NtsServicesFactory.cs` が生成し、テスト/ベンチは `Samples.Services` / `FixtureSource.Services` 経由で共有する。

### 守るべき NTS 互換の癖

正典は `docs/nts-quirks.md`。2 度踏んだ罠がコード内に残っているので知っておく:

1. **MultiPoint WKB ヘッダに Z/M オフセットを付けない** — `ZWkbWriter` は MultiPoint だけ外側タイプコードを常に `4` で書く（NTS の SFS-1.1 流儀に合わせる）。他の Multi\*/GC は 1005 / 1006 / 1007 等のオフセット付き。`ZWkbWriter.WriteGeometry` の `suppressOrdOffset = g is MultiPoint` 分岐を参照
2. **コンテナの `HasZ` / `HasM` 判定は型で再帰する** — `NtsWkbWriter` は `is MultiPoint or MultiLineString or MultiPolygon or GeometryCollection` で分岐している。`g.NumGeometries > 1` で分岐すると子が 1 つだけの Multi\* で Z/M が黙って消えるバグになる

## テスト

`tests/ZeroWkX.Tests/` 配下、合計 **1,122 件**が NTS をオラクルに比較する。構成:

| ファイル | 役割 |
|---------|------|
| `NaiveWktTests` / `NaiveWkbTests` / `ZV{1..3}Tests` / `ZReaderTests` | 手書きフィクスチャ（`Fixtures/Samples.cs`）での実装別 NTS 同値確認 |
| `PropertyBasedTests` | seeded ジェネレータ（`Fixtures/GeometryGenerator.cs`）× 全実装。カバレッジの主柱 |
| `WriterInteropTests` | 全 Writer × 全 Reader のクロスラウンドトリップ |
| `NumericEdgeTests` | NaN / ±∞ / subnormal / -0.0 / 科学表記 / 大小文字・空白 |
| `MalformedInputTests` | WKT/WKB バリデーション。EWKB と truncate 入力は例外必須 |
| `EdgeCaseTests` | EMPTY / ネスト GC / 負のゼロ / EWKB 拒否 |
| `ZWriterTests` | NTS Reader で読み返すラウンドトリップ |

配列ループは `(string Name, Func<string, Geometry> Read, long Ulp)[]` / `(string Name, Func<byte[], Geometry> Read)[]` 形式のタプル（インターフェース抽象を廃止したため）。

### オラクル判定ルール

- 座標は `BitConverter.DoubleToInt64Bits` で**ビット単位比較**（`Fixtures/CoordinateAsserts.cs`）。V2/V3/Z の WKT テストのみ 1 ULP 許容（カスタム double パーサが BCL と独立に丸めるため）。WKB は全て 0 ULP
- **Writer 間でバイト単位一致を要求しない**。NTS には単一子 MultiPoint や SFS-1.1 コンテナ規則など模倣が面倒な癖がある。テストで担保するのは相互可読性（任意の Writer の出力を任意の Reader が同一ジオメトリに戻せること）
- ポリゴンリングの向き（CW/CCW）は絶対に正規化しないこと。正規化すると実バグを隠す

## ベンチマーク

`bench/ZeroWkX.Benchmarks/`、BenchmarkDotNet 使用。全 Reader ベンチは `[Benchmark(Baseline = true)] Nts()` を立てているので `Ratio` 列がそのまま「NTS 比」として読める。

- `WktReadBenchmarks` / `WkbReadBenchmarks` — メインマトリクス（6/4 実装 × 5 種 × 3 サイズ × {LE, BE}）
- `WktReadDimensionsBenchmarks` / `WkbReadDimensionsBenchmarks` — 次元ごとの勾配（XY / XYZ / XYM / XYZM）
- `RealisticShapeBenchmarks` — seeded 合成「海岸線的」MultiPolygon（準共線点、複数穴）
- `NaturalDataBenchmarks` — 実データ `bench/Data/N03_Kagawa.wkb` 使用（詳細は `bench/Data/README.md`）

`FixtureSource.cs` は共有状態。ジオメトリ生成ロジックの変更は全ベンチクラスに波及する。フィクスチャは決定論的（`Random(42)` 固定）なので、ラン間で比率を比較できるよう決定論性を崩さないこと。

## `bench/Data/` のライセンス

`bench/Data/` 配下のファイルは MIT / BSD ではなく、**国土数値情報**（国土交通省）の利用規約に基づく。要件は**出典明示**（`「国土数値情報（行政区域データ）」（国土交通省）`）。他のコードは BSD-3-Clause。実データを追加する場合は `bench/Data/README.md` に出典を追記し、コードのライセンス表記と分けて管理すること。

## スコープ

- **OGC ISO WKB のみ**。EWKB（PostGIS SRID/Z/M 高位ビット、マスク `0xE0000000`）は `FormatException` で明示的に拒否
- 空間演算（intersects, buffer 等）は扱わない。I/O のみ。演算は NTS に委譲
- `POINT EMPTY` の WKB は全 ordinates が NaN（OGC 仕様）。NTS もそう出力・解釈する

## 関連ドキュメント

- `docs/nts-quirks.md` — 実装過程で判明した NTS 2.6 の未文書化挙動集。Writer のタイプコード・次元処理・WKT トークナイズを触る前に必読
- `TODO.md` — 運用系の残タスク（NuGet パッケージング、CI、任意のベンチ拡張）。機能実装は完了している
