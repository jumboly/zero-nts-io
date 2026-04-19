# TODO

残作業を用途別にまとめる。機能的な実装タスクは無く、以下は全て「状況次第で着手」のもの。

## 公開するなら（blocking for publication）

- [x] `src/ZeroNtsIo/ZeroNtsIo.csproj` に NuGet metadata を追加（`<PackageId>ZeroNtsIo</PackageId>` は既設）
  - `<Version>0.1.0</Version>`
  - `<Description>...</Description>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
  - `<PackageTags>` も追加、`..\..\README.md` を `None Include` で同梱
- [x] 公開対象は `ZeroNtsIo` のみ。`ZeroNtsIo.Stages` / `ZeroNtsIo.Naive` / `ZeroNtsIo.Reference` に `<IsPackable>false</IsPackable>` を付与
- [x] `Directory.Build.props` の `<PackageProjectUrl>` を `https://github.com/jumboly/zero-nts-io` へ更新、`<RepositoryUrl>` / `<RepositoryType>` も追加
- [x] `CHANGELOG.md` を新設し `0.1.0` エントリを入れる

## CI を入れるなら

- [ ] `.github/workflows/ci.yml` で push / PR 時に `dotnet test -c Release` を実行
- [ ] Dependabot (`.github/dependabot.yml`) で NuGet 依存の自動更新
- [ ] GitHub の CodeQL デフォルトスキャン有効化

## ベンチマーク拡張（低優先）

- [ ] `WktReadBenchmarks` / `WkbReadBenchmarks` の `Nts_Default` バリアント再計測 + README 反映
  - コードは適用済み（`new WKTReader()` / `new WKBReader()` 相当、既定 `CoordinateArraySequenceFactory` 使用）
  - WkbRead は再計測済み（100k LineString LE で NTS 既定 2,752 µs / Packed 638 µs / ZeroNtsIo 90 µs、既定は Packed の 4 倍遅い）
  - WktRead は未計測（並列汚染で中断）。`dotnet run -c Release --project bench/ZeroNtsIo.Benchmarks -- --filter '*WktReadBenchmarks*' --job short` を他ベンチと並列にせず単独で流す
  - 揃ったら README の「ベンチマーク概要」「ベンチマーク結果」両セクションに Nts_Default 行を追加
- [ ] `WktWriteDimensionsBenchmarks` / `WkbWriteDimensionsBenchmarks` — Writer 側でも XY/XYZ/XYM/XYZM で計測
- [ ] 病的ケースベンチ
  - 1M 座標の LineString（キャッシュ外 / DRAM 律速領域）
  - 深いネスト `GeometryCollection`（再帰コスト）
  - 100 穴の Polygon（リング集計ループ）

## 検討中（要スコープ明確化）

着手前に「何を目指すか」を決める必要があるもの。`/plan` する際にサブタスクへ分解する。

- [x] **EWKB（PostGIS 形式）対応** — 公開版 `ZWkbReader` が SRID + 旧 PostGIS Z/M 高位ビットをフル受理、`ZWkbWriter.Write` に `handleSRID` オプション追加（`tests/ZeroNtsIo.Tests/EwkbTests.cs` が 33 ケース担保）。`NaiveWkbReader` / `ZWkbReaderV1` は教材用途で厳密 OGC ISO のまま

- [ ] **段階的バージョンの整理** — `src/ZeroNtsIo.Stages/` (V1/V2/V3) の扱いを決める
  - 選択肢: (a) 現状維持 / (b) 役割と寿命を `docs/` で明文化 / (c) V1-V3 を刈り込む（例: ベンチで説明に使う段だけ残す）
  - 論点: Stages はベンチで「各段の純効果」を見せる教材。将来のメンテ負担と学習価値のトレードオフ
- [ ] **ベンチマークの整理** — `§ベンチマーク拡張` とは別に、既存ベンチの構造を見直す
  - 選択肢: (a) ベンチクラスの重複削減・命名統一 / (b) `docs/bench-policy.md` と実ベンチの齟齬洗い出し / (c) `FixtureSource` 共有状態の整理
  - 論点: クラス数が増えたのでシナリオ別（日常/退行/リリース）に実行しやすい構成にしたい
- [ ] **README の整理** — 現状長め（ベンチ表 × 複数セクション）
  - 選択肢: (a) 節構成見直しのみ / (b) 詳細を `docs/` に移し README はピッチ + 代表数値 + リンク集 / (c) 英語版追加
  - 論点: NuGet ページに出るのは README の先頭付近。最初のスクロール 1 画面で「何者か / どれだけ速いか」が伝わるべき

## コード品質（細部）

- [ ] `Naive WKT Writer` の NaN / Infinity 出力が NTS と完全一致するかの直接テスト（現状は Reader round-trip での間接検証のみ）
- [ ] `src/ZeroNtsIo/Internal/PooledDoubleBuffer.cs` の `Finish()` 内のコピー削減余地を調査
  - WKB Reader は count を事前に読めるので `new double[count * dim]` で pool を経由しない（現状すでに最適）
  - WKT Reader は count 未知のため pool で成長させる必要あり。`Finish()` で `ArrayPool` の over-sized 配列から right-sized 配列へ 1 回コピーしている（`PackedCoordinateSequenceFactory` は所有権を受け取り解放できないため pool 配列を直接渡せない）
  - 候補: (a) WKT も事前に座標数をカウントする 2-pass 解析（小さい geometry では退行リスク）／(b) `_count == _buf.Length` の場合のみコピー省略（pool が右サイズ返す保証は無いので効果限定的）／(c) WKT でも容易にサイズが分かるケース（単一 POINT、ring prefix ヒューリスティクス）だけ特別パス
  - 実益計測は `WktReadBenchmarks` の Allocated 列で確認

---

運用メモ:
- GitHub に push したらこのファイルの内容を Issues に分解・移行すると追跡性が上がる
- 着手時は該当項目を `- [x]` にするか、diff を残したい場合は削除してコミットメッセージで言及
