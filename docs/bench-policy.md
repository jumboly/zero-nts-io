# ベンチマーク運用方針

`bench/ZeroWkX.Benchmarks/` の 8 クラスをどう使い分けるか、どの軸がどれだけ信号を持つか、削減候補はどこかをまとめる。設計意図は `CLAUDE.md` の「段階最適化の階段」節と対応。

## ベンチクラスの位置付け

| 優先度 | クラス | 役割 | ケース数 | 目安時間（short） |
|---|---|---|---:|---:|
| ★★★ | `WktReadBenchmarks` | V1→V2→V3→Fast の 4 段階の階段を唯一可視化 | 245 | 約 30 分 |
| ★★★ | `WkbReadBenchmarks` | packed factory 効果 + SIMD (LE/BE) 経路の検証 | 350 | 約 40 分 |
| ★★ | `WkbReadDimensionsBenchmarks` | 次元 × SIMD の組み合わせ回帰検知 | 24 | 数分 |
| ★★ | `WktReadDimensionsBenchmarks` | packed factory の次元スケーリング | 12 | 1 分 |
| ★★ | `NaturalDataBenchmarks` | 実 GPS 分布での FastDoubleParser 検証 + 対外信頼性 | 約 14 | 2 分 |
| ★ | `WkbWriteBenchmarks` | NTS vs Fast 2 者比較のみ（段階なし） | 210 | 約 25 分 |
| ★ | `WktWriteBenchmarks` | 同上、`double.ToString` 律速で差が小さい | 105 | 約 12 分 |
| ★ | `RealisticShapeBenchmarks` | 合成「海岸線風」形状、NaturalData と重複気味 | 約 11 | 1 分 |

全クラス合計 約 971 ケース / ラン時間 **約 2 時間（`--job short`）**、`dry` なら約 15 分、デフォルト job なら約 8 時間。

## 軸の評価

### Coords `[1, 10, 100, 1_000, 10_000]`
直近ラン（WkbRead Nts_Default ratio）データに基づく:

| 値 | 信号内容 | 情報量 |
|---|---|---|
| 1 | 純オーバーヘッド（ヘッダ / オブジェクト構築） | ★ |
| 10 | 小サイズ遷移期（per-op と per-coord 拮抗） | ★★ |
| 100 | **主要遷移点**（packed factory が効き始める） | ★★★ |
| 1000 | 漸近線接近 | ★★ |
| 10000 | 漸近線到達 + GC Gen1/Gen2 | ★★★ |

冗長ペア: `1 と 10`（両方 overhead 領域）、`1000 と 10000`（比率変化が鈍い）。3 値に絞るなら `[1, 100, 10000]` が最適（log 等間隔 + 遷移点を含む）。

### Kind `[Point, MultiPoint, LineString, Polygon, PolygonWithHoles, MultiPolygon, GeometryCollection]`
同じ Coords=10000 でも ratio が 1.05-1.98 と 2 倍散らばる **強い信号軸**。ただし内部重複あり:
- `Polygon` ≒ `PolygonWithHoles` — ring 構造が同じ
- `MultiPolygon` ⊂ `GeometryCollection` — 後者が前者を包含
- `Point` は Coords 無効化（単点固定）

### Order `[LittleEndian, BigEndian]`
WkbRead / WkbWrite / Dimensions / Natural に存在。Coords≥100 でのみ差が出る SIMD 経路検証軸。回帰検知価値は高いが、Coords=1 との組み合わせは冗長。

### Ordinates `[XY, XYZ, XYM, XYZM]`
`*Dimensions*Benchmarks` クラスにのみ存在。packed factory が次元増加で線形スケールするかの検証。

### Method（ベンチごとに 3-7）
最も削減効果が低い軸（1 メソッド削除で 4-8%）。ただし用途別に限定すれば意味ある削減になる:
- `Naive` は **オラクル補助が本来の役割**、perf ベンチから外して可
- `Nts_Default` は XY 固定の WktRead / WkbRead にのみ存在。Dimensions / Natural / Realistic には無いため一見不統一だが、**日常ランの主軸として残す**（現実の NTS ユーザー多数派が既定 services = XY で使うため。`docs/bench-policy.md` の「日常ラン」参照）
- `V1 / V2 / V3` は段階差分ベンチの本質、退行疑い時以外は除外可

## 軸削減のインパクト

ラン時間削減効果が大きい順:

| 操作 | 削減率 | 残り時間 | 失う情報 |
|---|---:|---:|---|
| Write クラス 2 本を除外 | **-35%** | 120→78 分 | Writer 回帰の網羅確認 |
| Coords を `[1, 100, 10000]` の 3 値に | -20% | 120→96 分 | 漸近過程の勾配 |
| Order を LE のみに | -31% | 120→83 分 | SIMD (BE) 経路検証 |
| Naive / Nts_Default 削除 | -8% | 120→110 分 | Naive はオラクル補助で代替、Nts_Default は不統一 |
| V1/V2/V3 を除外（filter で対応可能） | 可変 | 可変 | 段階差分の階段 |

## シナリオ別運用

### 日常ラン（最適化開発中、XY 実用比較、約 15 分）
```bash
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- \
  --filter '*ReadBenchmarks.Nts*' '*ReadBenchmarks.Fast' --job short
```
`Nts_Default` / `Nts` / `Fast` の 3 本、XY 固定（`*ReadBenchmarks` で `Dimensions` クラスを除外、主 Read 2 本は Ordinates パラメータなしで XY のみ）。**`Nts_Default` を主軸に据えるのが本方針の核**: NTS ユーザーの多数派は `new WKTReader()` / `new WKBReader()` など**既定 services（`CoordinateArraySequenceFactory`, XY 専用）** で使っており、packed factory を明示設定している `Nts` より `Nts_Default` との比較こそが現実のユーザー体験を表す。即確認したいだけなら `--job dry` に切り替え（数分、絶対値は信頼しない）。

### 開発ラン（段階最適化のチューニング、約 70 分）
```bash
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- \
  --filter '*WktRead*' '*WkbRead*' --job short
```
Read 2 本だけ full。全 Method 含む 4 段階差分（V1→V2→V3→Fast）が見える。Dimensions も含む。

### 退行検知ラン（リリース前 / PR 前、1 時間程度）
```bash
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- \
  --filter '*Read*' '*Natural*' --job short
```
Write 系を除外、次元ベンチと実データを含む網羅。

### 対外レポートラン（公開 / バージョンリリース時、2 時間）
```bash
dotnet run -c Release --project bench/ZeroWkX.Benchmarks -- \
  --filter '*' --job short
```
全クラス全ケース。`BenchmarkDotNet.Artifacts/results/` 配下の `*-report-github.md` を README 等に貼る。

## 削減の優先順位

1. **Write 2 本をデフォルトから除外** — 効果最大、失う情報少ない
2. **Coords を 3 値に** — 勾配情報を少し失うが 20% 削減
3. **Method は filter で絞る**（ベンチ定義は変えない、呼び出し側で制御）
4. **Order と Kind は残す** — 軸自体の信号価値が高い
