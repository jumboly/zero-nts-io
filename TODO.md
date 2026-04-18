# TODO

残作業を用途別にまとめる。機能的な実装タスクは無く、以下は全て「状況次第で着手」のもの。

## 公開するなら（blocking for publication）

- [ ] `src/ZeroWkX/ZeroWkX.csproj` に NuGet metadata を追加（`<PackageId>ZeroWkX</PackageId>` は既設）
  - `<Version>0.1.0</Version>`
  - `<Description>...</Description>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
- [ ] 公開対象は `ZeroWkX` のみ。`ZeroWkX.Stages` / `ZeroWkX.Naive` / `ZeroWkX.Reference` は `<IsPackable>false</IsPackable>` を付与（現在は未設定だが NuGet に出ない保険）
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

---

運用メモ:
- GitHub に push したらこのファイルの内容を Issues に分解・移行すると追跡性が上がる
- 着手時は該当項目を `- [x]` にするか、diff を残したい場合は削除してコミットメッセージで言及
