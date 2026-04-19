# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## コマンド

```bash
# ビルド（Release が基本、net10.0）
dotnet build -c Release

# 全テスト（1254/1254 通過が期待値。退行は即バグ）
dotnet test tests/ZeroNtsIo.Tests -c Release

# 1 クラスだけ
dotnet test tests/ZeroNtsIo.Tests -c Release --filter "FullyQualifiedName~ZReaderTests"

# 1 ケースだけ
dotnet test tests/ZeroNtsIo.Tests -c Release --filter "FullyQualifiedName~EdgeCaseTests.Nested_GeometryCollection"

# ベンチ（部分実行推奨、--filter '*' は M 系で 30 分超）
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*WkbRead*' --job short
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*Dimensions*' --job short
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*NaturalData*' --job short
```

実測せずスモーク確認したい時は `--job dry`。

## アーキテクチャ

本ライブラリは **NetTopologySuite の `Geometry` を入出力する WKT/WKB I/O のドロップイン置換**であり、独自のジオメトリモデルは持たない。Reader/Writer の API を触るとき、境界で NTS 型を保つ方針を崩さないこと。

公開クラスは `namespace ZeroNtsIo` 直下（NTS 公式 `NetTopologySuite.IO.WKTReader` / `WKBReader` とは完全修飾名で非衝突）。NuGet `PackageId` は `ZeroNtsIo`。ディレクトリ・プロジェクト名・リポジトリルート・namespace すべて `ZeroNtsIo.*` で統一。将来 GeoJSON / ShapeFile などの高速 IO シリーズを同ブランドで展開する前提の設計。

### プロジェクト構成

- **`src/ZeroNtsIo/`** — 唯一の公開パッケージ。`ZWktReader` / `ZWkbReader` / `ZWktWriter` / `ZWkbWriter` の 4 つの sealed class を `namespace ZeroNtsIo` 直下に公開。インターフェース抽象は持たない（`InternalsVisibleTo` で Stages/Tests/Benchmarks に internal ヘルパを見せる）。
- **`src/ZeroNtsIo.Stages/`** — 段階差分ベンチ専用の非公開プロジェクト。`ZWktReaderV1` / `ZWktReaderV2` / `ZWktReaderV3` / `ZWkbReaderV1` のみを持つ（V4 は公開版 `ZeroNtsIo` に移された）。`ZeroNtsIo` を ProjectReference し、共通 internal (`WktCursor` / `FastDoubleParser`) を借用する。
- **`src/ZeroNtsIo.Naive/`** — `string.Split` / `BinaryReader` による素朴実装。比較ベンチとオラクル補助用の非公開プロジェクト。
- **`src/ZeroNtsIo.Reference/`** — NTS 公式 IO の薄いラッパー。`NtsServicesFactory.CreatePacked()` がここに住み、`PackedCoordinateSequenceFactory.DoubleFactory` を差し込んだ `NtsGeometryServices` を供給する。非公開プロジェクト。

Test / Bench は 4 プロジェクト全部に ProjectReference する。

### 段階最適化の階段

公開版 `ZWktReader` / `ZWkbReader` は下記 4 段の最適化を**重ねた最終形**。`ZeroNtsIo.Stages` の V1/V2/V3 + 公開版で 4 段が揃い、ベンチマークで各段の純効果を見せる（各段で最適化手法を 1 つだけ積み足す設計のため、**BenchmarkDotNet の隣り合う行の差分がその手法の純効果**になる）:

- **V1**: `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` 入力、中間文字列ゼロ、`double.TryParse` は BCL 標準のまま
- **V2**（WKT のみ）: `Internal/FastDoubleParser.cs` を追加。ASCII 限定 `[+-]?\d+(\.\d+)?([eE][+-]?\d+)?` に mantissa `ulong` × `Pow10[]` テーブル乗算。mantissa > 2^53 / |exp| > 22 のときは BCL フォールバック
- **V3**（WKT のみ）: `Internal/PooledCoordinateBuffer` を追加。`ArrayPool` はスクラッチ用のみ（詳細は後述）
- **Z**（旧 V4、公開版）: `Internal/CoordinateBlockReader.cs`（LE = `MemoryMarshal.Cast` 再解釈、BE = `Vector128.Shuffle` バイトスワップ）と `Internal/PackedSequenceBuilder.cs`（`PackedCoordinateSequenceFactory.Create(double[], dim, measures)` に所有権渡しでゼロコピー）+ `PooledDoubleBuffer`

WKB には V1 と Z しか無い。テキスト解析が無く V2/V3 の最適化が効かないため。Writer は Z 1 本（段階ごとの差がベンチで見えない）。

**命名の注記**: `FastDoubleParser` クラスの `Fast` は業界標準名 `fast_float` 由来の技術用語であり、ブランドの `Fast` とは別概念。ブランドを `Z`/`ZeroNtsIo` に変更しても `FastDoubleParser` の名前は保つ。

### Internal ヘルパの所在

公開版の最適化に必要なヘルパは `src/ZeroNtsIo/Internal/`（namespace `ZeroNtsIo.Internal`）に置く:

- `WktCursor.cs` — span ベースの WKT トークナイザ（V1-V3 と Z が共有）
- `FastDoubleParser.cs` — カスタム double パーサ（V2/V3 と Z が共有）
- `PackedSequenceBuilder.cs` — `PackedCoordinateSequenceFactory` への所有権渡し（Z のみ使用）
- `PooledDoubleBuffer.cs` — `double[]` 用 ArrayPool ラッパー（Z のみ使用）
- `CoordinateBlockReader.cs` — SIMD バイトスワップ（Z WKB Reader / Writer のみ使用）

V3 専用の `PooledCoordinateBuffer.cs` は `src/ZeroNtsIo.Stages/Internal/`（namespace `ZeroNtsIo.Stages.Internal`）に残している。`ZeroNtsIo.Stages` は `ZeroNtsIo` への ProjectReference + `InternalsVisibleTo("ZeroNtsIo.Stages")` で公開プロジェクトの internal ヘルパにアクセスする。

### ArrayPool の適用範囲

`PackedCoordinateSequenceFactory.Create(double[], dim, measures)` は**配列の所有権を受け取り**、ジオメトリ寿命中に保持し続ける。したがって**最終座標配列は ArrayPool から取れない**。V3 はスクラッチ（リング集計配列・座標成長バッファ）のみを pool 化。公開版 Z は最終 `double[]` をヒープ確保しつつ `Coordinate` 構造体を全く経由しない。

### SIMD / unsafe の選択理由

`Vector128.Shuffle` を使う理由は Apple Silicon（ARM `tbl`）と x86（`pshufb`）の双方にマップされるから（`Avx2.Shuffle` だと Mac でベンチが動かない）。WKB-LE は SIMD の出番なし（バイト列が既に `PackedDoubleCoordinateSequence` レイアウトなので単一 `memcpy`）。SIMD の価値は BE 系（`WkbReadBenchmarks` / `WkbReadDimensionsBenchmarks` / `NaturalDataBenchmarks` の BE パラメータ）でのみ現れる。

### NtsGeometryServices の要件

全 Reader は `PackedCoordinateSequenceFactory.DoubleFactory` で構築した services を要求する。既定の `NtsGeometryServices.Instance` は `CoordinateArraySequenceFactory` で、packed ラウンドトリップ時に Z/M を黙って落とす。正しいインスタンスは `src/ZeroNtsIo.Reference/NtsServicesFactory.cs` が生成し、テスト/ベンチは `Samples.Services` / `FixtureSource.Services` 経由で共有する。

### 守るべき NTS 互換の癖

正典は `docs/nts-quirks.md`。2 度踏んだ罠がコード内に残っているので知っておく:

1. **MultiPoint WKB ヘッダに Z/M オフセットを付けない** — `ZWkbWriter` は MultiPoint だけ外側タイプコードを常に `4` で書く（NTS の SFS-1.1 流儀に合わせる）。他の Multi\*/GC は 1005 / 1006 / 1007 等のオフセット付き。`ZWkbWriter.WriteGeometry` の `suppressOrdOffset = g is MultiPoint` 分岐を参照
2. **コンテナの `HasZ` / `HasM` 判定は型で再帰する** — `NtsWkbWriter` は `is MultiPoint or MultiLineString or MultiPolygon or GeometryCollection` で分岐している。`g.NumGeometries > 1` で分岐すると子が 1 つだけの Multi\* で Z/M が黙って消えるバグになる

## テスト

`tests/ZeroNtsIo.Tests/` 配下、合計 **1,254 件**が NTS をオラクルに比較する。構成:

| ファイル | 役割 |
|---------|------|
| `NaiveWktTests` / `NaiveWkbTests` / `ZV{1..3}Tests` / `ZReaderTests` | 手書きフィクスチャ（`Fixtures/Samples.cs`）での実装別 NTS 同値確認 |
| `PropertyBasedTests` | seeded ジェネレータ（`Fixtures/GeometryGenerator.cs`）× 全実装。カバレッジの主柱 |
| `WriterInteropTests` | 全 Writer × 全 Reader のクロスラウンドトリップ |
| `NumericEdgeTests` | NaN / ±∞ / subnormal / -0.0 / 科学表記 / 大小文字・空白 |
| `MalformedInputTests` | WKT/WKB バリデーション。EWKB と truncate 入力は例外必須 |
| `EdgeCaseTests` | EMPTY / ネスト GC / 負のゼロ / EWKB 拒否 |
| `ZWriterTests` | NTS Reader で読み返すラウンドトリップ |
| `RegressionDataTests` | 国土数値情報（CC BY 4.0 範囲）の Point/Line/Polygon 計 8 フィクスチャ × 全 Reader/Writer。実データ退行の検知。`bench/Data/*.wkb` を `Fixtures/RealDataLoader.cs` で解決 |

配列ループは `(string Name, Func<string, Geometry> Read, long Ulp)[]` / `(string Name, Func<byte[], Geometry> Read)[]` 形式のタプル（インターフェース抽象を廃止したため）。

### オラクル判定ルール

- 座標は `BitConverter.DoubleToInt64Bits` で**ビット単位比較**（`Fixtures/CoordinateAsserts.cs`）。V2/V3/Z の WKT テストのみ 1 ULP 許容（カスタム double パーサが BCL と独立に丸めるため）。WKB は全て 0 ULP
- **Writer 間でバイト単位一致を要求しない**。NTS には単一子 MultiPoint や SFS-1.1 コンテナ規則など模倣が面倒な癖がある。テストで担保するのは相互可読性（任意の Writer の出力を任意の Reader が同一ジオメトリに戻せること）
- ポリゴンリングの向き（CW/CCW）は絶対に正規化しないこと。正規化すると実バグを隠す

## ベンチマーク

`bench/ZeroNtsIo.Benchmarks/`、BenchmarkDotNet 使用。全 Reader ベンチは `[Benchmark(Baseline = true)] Nts()` を立てているので `Ratio` 列がそのまま「NTS 比」として読める。

- `WktReadBenchmarks` / `WkbReadBenchmarks` — メインマトリクス（7/5 実装 × 7 種 × 5 サイズ × {LE, BE}）
- `WktReadDimensionsBenchmarks` / `WkbReadDimensionsBenchmarks` — 次元ごとの勾配（XY / XYZ / XYM / XYZM）
- `RealisticShapeBenchmarks` — seeded 合成「海岸線的」MultiPolygon（準共線点、複数穴）
- `NaturalDataBenchmarks` — 実データ `bench/Data/A45_Kagawa_NationalForest.wkb` 使用（香川県 国有林野 300 features、MultiPolygon、CC BY 4.0）。詳細は `bench/Data/README.md`

`FixtureSource.cs` は共有状態。ジオメトリ生成ロジックの変更は全ベンチクラスに波及する。フィクスチャは決定論的（`Random(42)` 固定）なので、ラン間で比率を比較できるよう決定論性を崩さないこと。

ベンチの取捨選択・運用方針は `docs/bench-policy.md` にまとめている。全クラス・軸の位置付け、削減効果の試算、シナリオ別の実行パターン（日常 / 退行検知 / リリース）を参照。

## `bench/Data/` のライセンス

`bench/Data/` 配下のファイルは MIT / BSD ではなく、**国土数値情報**（国土交通省）由来で、すべて **CC BY 4.0**。出典明示のみ必要（表示条項）。`RegressionDataTests` と `NaturalDataBenchmarks` が使用する。

他のコードは BSD-3-Clause。実データを追加する場合は `bench/Data/README.md` に出典を追記し、`license_raw` を `ksj/catalog/datasets.yaml` から確認してライセンス条件を明示する（採用は純粋な `オープンデータ（CC_BY_4.0）` のみ、`一部制限` / `注意事項` / `申請等必要` 付きのものは除外。旧 N03 は国土地理院申請の注記があるため撤去済み）。

## スコープ

- **OGC ISO WKB のみ**。EWKB（PostGIS SRID/Z/M 高位ビット、マスク `0xE0000000`）は `FormatException` で明示的に拒否
- 空間演算（intersects, buffer 等）は扱わない。I/O のみ。演算は NTS に委譲
- `POINT EMPTY` の WKB は全 ordinates が NaN（OGC 仕様）。NTS もそう出力・解釈する

## 関連ドキュメント

- `docs/nts-quirks.md` — 実装過程で判明した NTS 2.6 の未文書化挙動集。Writer のタイプコード・次元処理・WKT トークナイズを触る前に必読
- `TODO.md` — 運用系の残タスク（NuGet パッケージング、CI、任意のベンチ拡張）。機能実装は完了している
