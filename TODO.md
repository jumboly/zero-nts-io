# TODO

残作業を用途別にまとめる。機能的な実装タスクは無く、以下は全て「状況次第で着手」のもの。

## 公開するなら（blocking for publication）

- [ ] `src/ZeroNtsIo/ZeroNtsIo.csproj` に NuGet metadata を追加（`<PackageId>ZeroNtsIo</PackageId>` は既設）
  - `<Version>0.1.0</Version>`
  - `<Description>...</Description>`
  - `<PackageReadmeFile>README.md</PackageReadmeFile>`
- [ ] 公開対象は `ZeroNtsIo` のみ。`ZeroNtsIo.Stages` / `ZeroNtsIo.Naive` / `ZeroNtsIo.Reference` は `<IsPackable>false</IsPackable>` を付与（現在は未設定だが NuGet に出ない保険）
- [ ] `Directory.Build.props` の `<PackageProjectUrl>` を実際の公開先 URL に更新（現状 placeholder `https://github.com/`）
- [ ] `CHANGELOG.md` を新設し `0.1.0` エントリを入れる

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
