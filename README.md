# ZeroNtsIo

NetTopologySuite (NTS) 互換の高速 WKT / WKB 読み書きライブラリ（.NET 10 / C#）。

同じ `NetTopologySuite.Geometries.Geometry` を入出力するドロップイン実装で、**NTS 公式 `WKTReader` より 3〜4 倍速、公式 `WKBReader` より 5〜20 倍速**、アロケーションは最大で **96% 削減**されます。

`PackedCoordinateSequence` へのゼロコピー所有権渡しを軸にしており、パッケージ名の `Zero` はそこから。`WkX` は **W**ell-known (WKT + WKB) I/O の略。

---

## ベンチマーク概要

NTS 公式 Reader と本ライブラリ最終版 (`ZWktReader` / `ZWkbReader`) を LineString（Little Endian）で件数別に比較。`Nts_Default` は `new WKBReader()` / `new WKTReader()` と同じ既定 services (`CoordinateArraySequenceFactory`、XY 専用) を使う一般的な構成、`Nts` は `PackedCoordinateSequenceFactory` を供給した services を使う NTS の XYZ/XYM 対応構成。どちらも NTS 公式クラスをそのまま呼んでいる。

### WKB Read

| 件数 | Nts_Default | Nts (Packed) | **ZeroNtsIo** | 対 Default | 対 Nts |
|-----:|------------:|-------------:|------------:|-----------:|-------:|
|     1 |    64.7 ns |      58.2 ns |   **18.3 ns** |   3.5× |  3.2× |
|    10 |     147 ns |       105 ns |   **23.4 ns** |   6.3× |  4.5× |
|   100 |   1,056 ns |       602 ns |   **75.7 ns** |  13.9× |  7.9× |
| 1,000 |   10.1 µs |      5.56 µs |   **0.52 µs** |  19.6× | 10.8× |
| 10,000 |    110 µs |      63.8 µs |   **12.8 µs** |   8.6× |  5.0× |

### WKT Read

| 件数 | Nts_Default | Nts (Packed) | **ZeroNtsIo** | 対 Default | 対 Nts |
|-----:|------------:|-------------:|------------:|-----------:|-------:|
|     1 |     970 ns |       981 ns |    **221 ns** | 4.4× | 4.4× |
|    10 |   3.77 µs |      3.76 µs |    **867 ns** | 4.4× | 4.3× |
|   100 |   34.4 µs |      34.3 µs |   **8.85 µs** | 3.9× | 3.9× |
| 1,000 |    351 µs |       352 µs |    **113 µs** | 3.1× | 3.1× |
| 10,000 |   4.24 ms |     4.26 ms |   **1.18 ms** | 3.6× | 3.6× |

### 実データ（香川県 国有林野 300 MultiPolygon, CC BY 4.0）

| 対象 | Nts (Packed) | **ZeroNtsIo** | 倍速 |
|------|------------:|------------:|-----:|
| WKB Read LE | 352 µs | **77.9 µs** | 4.5× |
| WKB Read BE | 416 µs | **91.4 µs** | 4.6× |
| WKT Read LE | 17.1 ms | **5.88 ms** | 2.9× |
| WKT Read BE | 21.8 ms | **6.05 ms** | 3.6× |

**要点**:
- WKB では 1,000 件 LineString で **最大 19.6×** の速度差。OGC WKB-LE の座標ブロックが既に packed `double[]` レイアウトなので、SIMD も要らず `MemoryMarshal.Cast` 再解釈 + 1 回 `memcpy` で `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` に所有権渡しできるのが効く。10,000 件は双方とも LOH 確保で比率が縮む
- WKT は約 3〜4×。`double.TryParse` 支配域で、カスタム ASCII double パーサ + packed 直挿入 + ArrayPool スクラッチが効く
- アロケーションは WKT 10,000 件で 3,812 KB → 161 KB（96% 減）。WKT Fast は NTS 既定比 4% まで縮む

他ジオメトリ型 / BE / 段階実装（V1〜V3）の分解は下の「ベンチマーク結果」を参照。

---

## 構成

```
ZeroNtsIo.slnx
├─ src/
│   ├─ ZeroNtsIo/                # 公開パッケージ。ZWktReader / ZWkbReader / ZWktWriter / ZWkbWriter
│   ├─ ZeroNtsIo.Reference/      # NTS 公式 IO の薄いラッパー（比較ベースライン、NtsServicesFactory 提供）
│   ├─ ZeroNtsIo.Naive/          # string.Split / BinaryReader による素朴実装（比較用）
│   └─ ZeroNtsIo.Stages/         # V1..V3 段階実装（ベンチマークで各最適化手法の純効果を見せるためのみ）
├─ tests/ZeroNtsIo.Tests/        # xUnit：NTS 出力とのビット単位比較、1254 件
└─ bench/ZeroNtsIo.Benchmarks/   # BenchmarkDotNet
```

`ZeroNtsIo` のみが利用者向け API。公開クラスは `namespace ZeroNtsIo` 直下に並び、`using ZeroNtsIo;` 1 行でドロップイン代替を可能にしている（NTS 公式 `NetTopologySuite.IO.WKTReader` / `WKBReader` とは完全修飾名で非衝突）。将来 `ZeroNtsIo.GeoJson` / `ZeroNtsIo.ShapeFile` 等の高速 IO シリーズを同ブランドで追加する前提の設計。`ZeroNtsIo.Stages` / `ZeroNtsIo.Naive` / `ZeroNtsIo.Reference` はベンチ・テスト専用で、パッケージとしての公開対象外。

### 出力型

すべての実装が `NetTopologySuite.Geometries.Geometry` を返すので、既存の NTS パイプライン（空間演算、GeoJSON 変換、spatial index 等）にそのまま差し替え可能です。独自の POCO は作りません。

### 対応範囲

| 分類 | 対応 |
|------|------|
| ジオメトリ型 | Point / LineString / Polygon / Multi* / GeometryCollection |
| 次元 | XY / XYZ / XYM / XYZM |
| WKB | OGC ISO（型コード 1001/2001/3001 等） LE / BE 両対応 |
| WKT | 標準 + 大文字小文字・空白揺れ・`EMPTY` 許容 |
| EWKB | PostGIS SRID 埋め込み + 旧 PostGIS Z/M 高位ビットを Reader が受理、`ZWkbWriter.Write(g, bo, handleSRID: true)` で SRID 付き出力 |

---

## セットアップ

```bash
# ビルド
dotnet build -c Release

# テスト（NTS とのビット単位一致を検証、全 1254 件）
dotnet test -c Release

# ベンチマーク（全組み合わせ、`--job short` で約 2 時間）
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*' --job short

# 部分実行
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*WkbRead*' --job short
dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*WktRead*' --job short
```

---

## 使い方

```csharp
using ZeroNtsIo;
using NetTopologySuite;
using NetTopologySuite.Geometries.Implementation;

// PackedCoordinateSequenceFactory が前提（ゼロコピー経路の要件）
var services = new NtsGeometryServices(PackedCoordinateSequenceFactory.DoubleFactory);

var reader = new ZWkbReader(services);
var writer = new ZWkbWriter();

Geometry g = reader.Read(wkbBytes);
byte[] bytes = writer.Write(g);  // LE がデフォルト
```

WKT も同じ形:

```csharp
var wktReader = new ZWktReader(services);
var wktWriter = new ZWktWriter();

Geometry g = wktReader.Read("POINT Z (1 2 3)");
string text = wktWriter.Write(g);
```

---

## ベンチマーク結果

計測環境: macOS Tahoe 26.4.1 / Apple A18 Pro (6 cores) / .NET SDK 10.0.201 (.NET 10.0.5 Arm64 RyuJIT) / BenchmarkDotNet v0.15.8 `--job short` (3 iterations × 3 warmups)。NTS バージョンは 2.6。座標は seed 固定の決定論ジェネレータで生成し、ラン間で同じ入力を保証。全数値は `BenchmarkDotNet.Artifacts/results/*-report-github.md` の該当行から抜粋。

### WKT Read, 10,000 coords LineString — 段階差分

`ReadOnlySpan<char>` ベース段階実装と NTS 公式 `WKTReader` の比較。隣り合う行の差が各最適化手法の純効果に対応する。

| 実装 | Mean (ms) | Ratio | Allocated | Alloc Ratio | この段で積んだ工夫 |
|------|----------:|------:|----------:|------------:|--------------------|
| `Nts_Default` (既定 services) | 4.244 | 1.00 | 3,812 KB | 1.00 | （ベースライン、`CoordinateArraySequenceFactory`） |
| `Nts` (Packed services) | 4.260 | 1.00 | 3,972 KB | 1.04 | （比較用、`PackedCoordinateSequenceFactory`） |
| `Naive` | 2.282 | 0.54 | 2,049 KB | 0.52 | （比較用、`string.Split` ベース） |
| `V1_Span` | 1.633 | 0.38 | 823 KB | 0.21 | **Span 化：−649 µs、−1,226 KB** |
| `V2_CustomParser` | 1.310 | 0.31 | 823 KB | 0.21 | **カスタム double パーサ：−323 µs**（メモリ据え置き） |
| `V3_ArrayPool` | 1.274 | 0.30 | 560 KB | 0.14 | **ArrayPool（スクラッチのみ）：−263 KB**（時間は誤差） |
| **`Z` (最終版)** | **1.179** | **0.28** | **161 KB** | **0.04** | **packed 直挿入 + unsafe：−95 µs、−399 KB** |

右端の「この段で積んだ工夫」は 1 段上との差分（純効果）。各段階で 1 つの最適化しか積み足さない設計のためこの引き算が成立する。

### WKB Read, 10,000 coords LineString — 段階差分

WKB には V2/V3 がない（double 文字列の解析がないため custom parser も ArrayPool もテキストバッファに効かない）。

| 実装 | LE Mean (µs) | BE Mean (µs) | Ratio (LE) | Allocated | この段で積んだ工夫 |
|------|-------------:|-------------:|-----------:|----------:|--------------------|
| `Nts_Default` | 110 | 120 | 1.72 | 391 KB | （ベースライン） |
| `Nts` (Packed) | 63.8 | 75.2 | 1.00 | 156 KB | （公式 + Packed services、比較軸） |
| `Naive` | 184 | 184 | 2.88 | 547 KB | （`BinaryReader.ReadDouble` 逐次） |
| `V1_Span` | 123 | 128 | 1.93 | 547 KB | **Span 化：−61 µs**（Naive 比） |
| **`Z` (最終版)** | **12.8** | **15.3** | **0.20** | **156 KB** | **LE: `MemoryMarshal.Cast` 再解釈 + `memcpy` / BE: `Vector128.Shuffle` で 16 B SIMD バイトスワップ：−110 µs**（V1 比） |

LE と BE の差（12.8 µs vs 15.3 µs ≈ 2.5 µs）が SIMD バイトスワップのコスト。LE では `Vector128.Shuffle` の出番がなく（既に packed レイアウト）、一発 `memcpy` で済む。

### 各最適化手法の中身

最終実装は下記 4 段の最適化を重ねたもの。段階差分ベンチ (`ZeroNtsIo.Stages` + `ZeroNtsIo` の比較) で各寄与を個別計測できる。

| 段階 | 具体策 | 主要な効く場所 | 上位実装との差（10k LineString） |
|------|--------|----------------|----------------------------------|
| **V1: Span** | 入力を `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` で受け、中間文字列を作らない。トークン境界は `IndexOf` / `Slice` で取る | WKT / WKB 共通の基盤改善 | WKT: −649 µs / WKB: −61 µs |
| **V2: カスタム double パーサ** | ASCII 限定 `[+-]?\d+(\.\d+)?([eE][+-]?\d+)?` を mantissa `ulong` × `Pow10[]` テーブル乗算で解く。mantissa > 2^53 / \|exp\| > 22 で BCL にフォールバック | WKT のみ（WKB には double 文字列がない） | WKT: −323 µs |
| **V3: ArrayPool（スクラッチのみ）** | リング集計配列・座標成長バッファを `ArrayPool<T>` からリース。最終座標配列は packed factory が所有権を取るため pool 経由にできない | WKT メモリ（時間効果は小さい） | WKT: −263 KB（時間ほぼ無差） |
| **Z (= 公開版)** | **WKB-LE**: `MemoryMarshal.Cast<byte, double>` で `ReadOnlySpan<byte>` をそのまま `double[]` 化相当にして 1 回 `memcpy`。**WKB-BE**: `Vector128.Shuffle` で 16 バイトごとに SIMD バイトスワップ（x86 `pshufb` / ARM `tbl` に JIT がマップ）。**WKT / WKB 共通**: `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` に `double[]` の**所有権を渡す**ことで `Coordinate` 構造体への詰め替えを完全回避 | WKB 大勝ち / WKT はアロケ圧縮と最終挿入 | WKT: −95 µs, −399 KB / WKB: −110 µs |

#### 純効果の算出

同じ `[Params]` の行で隣同士を引き算することで、各最適化手法の寄与が独立に出る:

- `V1 − Naive` = Span 化の効果（入力受け取り・トークナイズ）
- `V2 − V1` = カスタム double パーサの効果（WKT のみ）
- `V3 − V2` = ArrayPool の効果（ほぼメモリのみ）
- `Z − V3` = packed 直挿入 + unsafe / SIMD の効果（最終挿入とバイトスワップ）

### 他ジオメトリ型 / 他サイズの位置付け

`BenchmarkDotNet.Artifacts/results/` 配下に全クラスのレポートが保存されており、Kind `{Point, MultiPoint, LineString, Polygon, PolygonWithHoles, MultiPolygon, GeometryCollection}` × Coords `{1, 10, 100, 1,000, 10,000}` × Order `{LE, BE}` の完全マトリクスが見える。大きな傾向として:

- **WKB Read**: どの Kind / Coords でも `Z` が最速。1,000 件域で対 Nts_Default 約 10〜20× とスイートスポットに達し、10,000 件では LOH アロケが入るため倍率はやや縮む（絶対時間は一番速い）
- **WKB Write**: NTS vs `Z` の 2 者比較のみ。LE は `memcpy`、BE は `Vector128.Shuffle` で書き出しも 3〜5×
- **WKT Read**: 件数によらずほぼ 3〜4×。`double.TryParse` 支配だが V2 のカスタムパーサが効く
- **WKT Write**: NTS の `double.ToString("R")` が律速で、差は小さい（約 1.5〜2×）。Writer は 1 バリアントで十分な理由

実運用に近い形状として `NaturalDataBenchmarks` が国土数値情報（CC BY 4.0）の香川県 国有林野 300 MultiPolygon を使っており、冒頭「実データ」表の数値がそれ（`NaturalDataBenchmarks-report-github.md`）。

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

`bench/Data/` 配下の実地理データは国土数値情報（国土交通省）由来で、すべて **CC BY 4.0** のもとで配布されている（出典明示必須）。詳細は [`bench/Data/README.md`](bench/Data/README.md)。

## 関連ドキュメント

- [`docs/nts-quirks.md`](docs/nts-quirks.md) — 実装・テスト過程で判明した NTS 2.6 の仕様・挙動のまとめ。MultiPoint 外側タイプコードの特殊扱いや `NtsGeometryServices.Instance` の Z/M 落としなど、互換実装するなら必読

## 設計判断メモ

- **なぜ最終版とは別に V1〜V3 を残しているか**: 段階差分ベンチで各最適化手法の純効果を可視化するため（`Options` enum 切り替えだと分岐コストが内部ループに混ざる）。`ZeroNtsIo.Stages` は非公開で、`ZeroNtsIo` 本体のみが利用者向け。
- **なぜ `ArrayPool` を最終座標配列に使わないか**: `PackedCoordinateSequenceFactory.Create(double[], dim, measures)` は配列の**所有権を受け取る**ため、NTS の `Geometry` 寿命中は解放できない。スクラッチ（成長バッファ・リング集計）のみを pool 化。
- **なぜ WKT Writer / WKB Writer は 1 バリアントか**: 書き出しは構造が単純で、段階ごとの差がベンチで見えない。最終形 1 本に絞った。
- **なぜ `Vector128.Shuffle` を使い `Avx2.Shuffle` を使わないか**: `Vector128.Shuffle` はクロスプラットフォーム（x86 の `pshufb` / ARM の `tbl` にそれぞれマップされる）で、Apple Silicon を含めどこでも SIMD 経路を使える。AVX2 固定にするとベンチ環境を x86 に縛ることになる。
- **なぜ公開クラスを `namespace ZeroNtsIo` 直下に置くか**: シリーズ展開（GeoJSON / ShapeFile 等）を前提に、ブランド独自の namespace で統一するため。NTS 公式 `NetTopologySuite.IO.WKTReader` とは完全修飾名で非衝突、`using ZeroNtsIo;` 1 行でドロップイン代替できる。Internal ヘルパは `namespace ZeroNtsIo.Internal` に分離。
