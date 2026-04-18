# TODO

残作業を用途別にまとめる。機能的な実装タスクは無く、以下は全て「状況次第で着手」のもの。

## 公開するなら（blocking for publication）

- [ ] 各 csproj に NuGet metadata を追加
  - `<PackageId>FastNtsWk.Fast</PackageId>` 等、プロジェクトごとに
  - `<Version>0.1.0</Version>`
  - `<Description>...</Description>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
- [ ] `Directory.Build.props` の `<PackageProjectUrl>` を実際の公開先 URL に更新（現状 placeholder `https://github.com/`）
- [ ] `CHANGELOG.md` を新設し `0.1.0` エントリを入れる

## CI を入れるなら

- [ ] `.github/workflows/ci.yml` で push / PR 時に `dotnet test -c Release` を実行
- [ ] Dependabot (`.github/dependabot.yml`) で NuGet 依存の自動更新
- [ ] GitHub の CodeQL デフォルトスキャン有効化

## ベンチマーク拡張（低優先）

- [ ] `WktWriteDimensionsBenchmarks` / `WkbWriteDimensionsBenchmarks` — Writer 側でも XY/XYZ/XYM/XYZM で計測
- [ ] 病的ケースベンチ
  - 1M 座標の LineString（キャッシュ外 / DRAM 律速領域）
  - 深いネスト `GeometryCollection`（再帰コスト）
  - 100 穴の Polygon（リング集計ループ）

## コード品質（細部）

- [ ] `Naive WKT Writer` の NaN / Infinity 出力が NTS と完全一致するかの直接テスト（現状は Reader round-trip での間接検証のみ）
- [ ] `Abstractions.IWktReader.Read(string)` / `IWkbReader.Read(byte[])` のデフォルト実装が全実装でオーバーライドされ使われていない。削るか活用するか判断
- [ ] README / CLAUDE.md / `plans/nts-wkt-wkb-tingly-teacup.md` の記述ゆれ整理（実装が先行して更新が追いつかなかった箇所あり）

---

運用メモ:
- GitHub に push したらこのファイルの内容を Issues に分解・移行すると追跡性が上がる
- 着手時は該当項目を `- [x]` にするか、diff を残したい場合は削除してコミットメッセージで言及
