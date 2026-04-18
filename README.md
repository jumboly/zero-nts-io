# FastNtsWk

NetTopologySuite (NTS) 互換の高速 WKT / WKB 読み書きライブラリ（.NET 10 / C#）。

同じ `NetTopologySuite.Geometries.Geometry` を入出力するドロップイン実装で、**NTS 公式 `WKTReader` / `WKBReader` より 3〜7 倍速**、アロケーションは最大で **96% 削減**されます。

段階的な最適化手法を 4 バリアント（V1〜V4）で分離実装しており、各手法の寄与をベンチマークで個別に計測できます。

---

## 構成

```
FastNtsWk.sln
├─ src/
│   ├─ FastNtsWk.Abstractions/     # IWktReader / IWktWriter / IWkbReader / IWkbWriter
│   ├─ FastNtsWk.Reference/        # NTS 公式 IO の薄いラッパー（比較ベースライン）
│   ├─ FastNtsWk.Naive/            # string.Split / BinaryReader による素朴実装
│   └─ FastNtsWk.Fast/             # V1..V4 高速実装
│       ├─ FastWktReaderV1.cs      # Span ベース / ゼロアロケ
│       ├─ FastWktReaderV2.cs      # + カスタム double パーサ
│       ├─ FastWktReaderV3.cs      # + ArrayPool スクラッチ
│       ├─ FastWktReaderV4.cs      # + unsafe + PackedDoubleCoordinateSequence 直挿入
│       ├─ FastWkbReaderV1.cs      # Span ベース
│       ├─ FastWkbReaderV4.cs      # + Unsafe.CopyBlockUnaligned + Vector128 SIMD
│       ├─ FastWktWriter.cs        # stackalloc + double.TryFormat
│       └─ FastWkbWriter.cs        # ArrayBufferWriter + SIMD バイトスワップ
├─ tests/FastNtsWk.Tests/          # xUnit：NTS 出力とのビット単位比較
└─ bench/FastNtsWk.Benchmarks/     # BenchmarkDotNet
```

### 出力型

すべての実装が `NetTopologySuite.Geometries.Geometry` を返すので、既存の NTS パイプライン（空間演算、GeoJSON 変換、spatial index 等）にそのまま差し替え可能です。独自の POCO は作りません。

### 対応範囲

| 分類 | 対応 |
|------|------|
| ジオメトリ型 | Point / LineString / Polygon / Multi* / GeometryCollection |
| 次元 | XY / XYZ / XYM / XYZM |
| WKB | OGC ISO（型コード 1001/2001/3001 等） LE / BE 両対応 |
| WKT | 標準 + 大文字小文字・空白揺れ・`EMPTY` 許容 |
| 非対応 | EWKB (PostGIS SRID 埋め込み) は明示的に例外 |

---

## セットアップ

```bash
# ビルド
dotnet build -c Release

# テスト（NTS とのビット単位一致を検証、全 207 件）
dotnet test -c Release

# ベンチマーク（全組み合わせ、数十分）
dotnet run -c Release --project bench/FastNtsWk.Benchmarks -- --filter '*' --job short

# 部分実行
dotnet run -c Release --project bench/FastNtsWk.Benchmarks -- --filter '*WkbRead*' --job short
dotnet run -c Release --project bench/FastNtsWk.Benchmarks -- --filter '*WktRead*' --job short
```

---

## 使い方

```csharp
using FastNtsWk.Fast;
using NetTopologySuite;
using NetTopologySuite.Geometries.Implementation;

// PackedCoordinateSequenceFactory が前提（V4 のゼロコピー経路の要件）
var services = new NtsGeometryServices(PackedCoordinateSequenceFactory.DoubleFactory);

var reader = new FastWkbReaderV4(services);
var writer = new FastWkbWriter();

Geometry g = reader.Read(wkbBytes);
byte[] bytes = writer.Write(g);  // LE がデフォルト
```

---

## ベンチマーク結果

計測環境: macOS (Apple Silicon), .NET 10.0.201, BenchmarkDotNet `--job short`

### WKT Read, 100,000 coords LineString

| 実装 | Mean (ms) | Ratio vs NTS | Allocated | Alloc Ratio | 段階の寄与 |
|------|-----------|--------------|-----------|-------------|------------|
| `Nts` (公式) | 48.5 | 1.00 | 39.2 MB | 1.00 | — |
| `Naive` | 27.5 | 0.57 | 20.0 MB | 0.51 | — |
| `V1_Span` | 18.8 | 0.39 | 7.7 MB | 0.20 | **Span 化：-8.7 ms、-12 MB** |
| `V2_CustomParser` | 15.1 | 0.31 | 7.7 MB | 0.20 | **double パーサ：-3.7 ms** |
| `V3_ArrayPool` | 15.6 | 0.32 | 5.6 MB | 0.14 | **pool：-2 MB**（時間は誤差） |
| `V4_Packed` | **13.1** | **0.27** | **1.6 MB** | **0.04** | **直接 packed：-2.5 ms、-4 MB** |

→ **V4 は NTS 比 3.7 倍速、アロケ 96% 減**

### WKB Read, 100,000 coords LineString (LE)

| 実装 | Mean (µs) | Ratio vs NTS | Allocated |
|------|-----------|--------------|-----------|
| `Nts` (公式) | 620 | 1.00 | 1.60 MB |
| `Naive` | 4,089 | 6.59 | 5.60 MB |
| `V1_Span` | 3,365 | 5.43 | 5.60 MB |
| `V4_PackedSimd` | **85** | **0.14** | **1.60 MB** |

→ **V4 は NTS 比 7 倍速**（LE では `Unsafe.CopyBlockUnaligned` による 1 回の memcpy で座標ブロック全体を packed 配列に流し込むため）

### WKB Read, 100,000 coords LineString (BE)

| 実装 | Mean (µs) | Ratio vs NTS |
|------|-----------|--------------|
| `Nts` (公式) | 747 | 1.00 |
| `V4_PackedSimd` | **116** | **0.16** |

→ **V4 は NTS 比 6 倍速**（`Vector128.Shuffle` による SIMD バイトスワップが効く）

---

## 各最適化手法が何をしているか

| 手法 | 具体策 | 主要な効く場所 |
|------|--------|----------------|
| **V1: Span** | 入力を `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` で受け、中間文字列を作らない。トークン境界は `IndexOf` / `Slice` で取る | WKT / WKB 共通で基盤改善 |
| **V2: カスタム double パーサ** | ASCII 限定の `[+-]?\d+(\.\d+)?([eE][+-]?\d+)?` を ulong 累積で解く。`10^22` までは厳密、越えたら BCL にフォールバック | WKT のみ（WKB には double 文字列がない） |
| **V3: ArrayPool** | `List<Coordinate>` による倍倍成長を `ArrayPool<Coordinate>` / `ArrayPool<LinearRing>` のリースに置換 | 主にメモリ（時間効果は限定的） |
| **V4: unsafe + SIMD + packed 直挿入** | WKB-LE は `Unsafe.CopyBlockUnaligned` で 1 回の memcpy、BE は `Vector128.Shuffle` で 16 バイトごとに SIMD バイトスワップ。`double[]` を `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` に所有権渡しして `Coordinate` 構造体化を完全に回避 | WKB 大勝ち、WKT は主にアロケ削減 |

### 差分が各手法の純効果

同じ `[Params]` のベンチで隣同士を引き算すると、各最適化の寄与が個別に出ます。
- `V1 − Naive` = Span 化の効果
- `V2 − V1` = カスタム double パーサの効果
- `V3 − V2` = ArrayPool の効果（時間はほぼ ゼロ、メモリ 22% 減）
- `V4 − V3` = unsafe / SIMD / packed 直挿入の効果

---

## テスト方針

NTS 公式出力との**ビット単位の座標一致**を `BitConverter.DoubleToInt64Bits` で検証。V2 以降は `FastDoubleParser` の丸め誤差を考慮して 1 ULP まで許容。

エッジケース:
- `POINT EMPTY` / `POLYGON EMPTY`（WKB は全 NaN で往復）
- ネスト `GeometryCollection`
- 大文字小文字・タブ・改行混じり WKT
- `-0.0` のビット表現保持、`NaN` M 座標
- EWKB 高位ビットフラグの明示的拒否

---

## ライセンス

**BSD 3-Clause License**（依存先の NetTopologySuite 本家に合わせた）。詳細は [`LICENSE`](LICENSE)。

`bench/Data/` 配下の実地理データのみ、国土数値情報利用規約に基づく別ライセンスで配布されている（出典明示必須）。詳細は [`bench/Data/README.md`](bench/Data/README.md)。

## 関連ドキュメント

- [`docs/nts-quirks.md`](docs/nts-quirks.md) — 実装・テスト過程で判明した NTS 2.6 の仕様・挙動のまとめ。MultiPoint 外側タイプコードの特殊扱いや `NtsGeometryServices.Instance` の Z/M 落としなど、互換実装するなら必読

## 設計判断メモ

- **なぜ V1〜V4 を別クラスにしたか**: `FastOptions` enum で切り替える設計だと、分岐コストが測定対象の内部ループに混ざる。別クラスだと各ベンチメソッドが単一の具象リーダーを呼ぶだけになり、差分が純粋な最適化効果を示す。
- **なぜ `ArrayPool` を最終座標配列に使わないか**: `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` は配列の**所有権を受け取る**ため、NTS の `Geometry` 寿命中は解放できない。V3 は WKT トークン位置配列・リング集計など**スクラッチ**のみを pool 化。
- **なぜ WKT Writer / WKB Writer は 1 バリアントか**: 書き出しは構造が単純で、段階ごとの差がベンチで見えない。最終形 1 本に絞った。
- **なぜ `Vector128.Shuffle` を使い `Avx2.Shuffle` を使わないか**: `Vector128.Shuffle` はクロスプラットフォーム（x86 の `pshufb` / ARM の `tbl` にそれぞれマップされる）で、Apple Silicon を含めどこでも SIMD 経路を使える。AVX2 固定にするとベンチ環境を x86 に縛ることになる。
