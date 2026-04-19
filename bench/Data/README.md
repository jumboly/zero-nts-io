# bench/Data — 実地理データフィクスチャ

国土数値情報（国土交通省）から抽出した WKB フィクスチャを収めるディレクトリ。全ファイル **Creative Commons 表示 4.0 国際 (CC BY 4.0)**。出典明示（下記「出典」節）が必須。

## ファイル

`RegressionDataTests` がテスト用に、`NaturalDataBenchmarks` がベンチ用に使用する。Point / Line / Polygon を複数データセットで覆う。`A45_Kagawa_NationalForest` はテストとベンチで共用（読み取り速度測定と退行検知の両方）。

**サンプリング方針**: ファイルサイズ 1 MB を上限とし、全 features の WKB サイズが上限を超えるものは **stride サンプリング**（全 features を等間隔で K 件ごとに 1 件採用、K=`ceil(全サイズ / 1MB)`）。先頭 N 件ではなく全域から拾うため、ソース内コード順などによる先頭バイアスを避けつつ、代表性のある標本を保つ。

| ファイル | レイヤー | 年度 | scope | 内容 | features | stride | サイズ | 用途 |
|---------|---------|------|-------|------|---------:|-------:|-------:|------|
| `A45_Kagawa_NationalForest.wkb` / `.wkt.txt` | A45 国有林野 | 2019 | prefecture 37 (香川) | MultiPolygon | 641 / 1,281 | 2 | ~808 KB / ~1.9 MB | bench + test |
| `A38_Kagawa_MedicalArea.wkb` | A38 医療圏 | 2014 | prefecture 37 (香川) | MultiPolygon | 141 / 281 | 2 | ~788 KB | test |
| `A03_Kinki_UrbanArea.wkb` | A03 三大都市圏計画区域 | 2003 | urban_area KINKI | MultiPolygon | 148 / 1,916 | 13 | ~864 KB | test |
| `N13_Kagawa_Roads.wkb` | N13 道路 | 2024 | mesh1 5033 | MultiLineString | 4,847 / 256,864 | 53 | ~828 KB | test |
| `N02_Japan_Railways.wkb` | N02 鉄道 | 2024 | national | MultiLineString | 3,134 / 21,932 | 7 | ~880 KB | test |
| `P04_Kagawa_Medical.wkb` | P04 医療機関 | 2010 | prefecture 37 (香川) | MultiPoint | 1,289 / 1,289 | 1 | ~27 KB | test |
| `P05_Kagawa_MunicipalHall.wkb` | P05 市町村役場等及び公的集会施設 | 2022 | prefecture 37 (香川) | MultiPoint | 761 / 761 | 1 | ~16 KB | test |
| `P36_Kagawa_ExpresswayBusStops.wkb` | P36 高速バス停留所 | 2023 | prefecture 37 (香川) | MultiPoint | 131 / 131 | 1 | ~3 KB | test |

全て LittleEndian WKB。座標系は EPSG:6668（JGD2011）、例外として A03 2003 年度は EPSG:4301（Tokyo Datum）。座標系の違いは I/O ラウンドトリップと parse 速度の測定には影響しない。

## 出典

本表に相当するそれぞれの国土数値情報データセット。**利用時は下記の出典表示を必須とする**（CC BY 4.0 の表示条項）:

> 「国土数値情報（国有林野）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-A45.html
> 「国土数値情報（医療圏）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-A38-2020.html
> 「国土数値情報（三大都市圏計画区域）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-A03.html
> 「国土数値情報（道路）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-N13-2024.html
> 「国土数値情報（鉄道）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-N02-2024.html
> 「国土数値情報（医療機関）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-P04-2020.html
> 「国土数値情報（市町村役場等及び公的集会施設）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-P05-v2_0.html
> 「国土数値情報（高速バス停留所）（国土交通省）」 https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-P36-2023.html

## ライセンス

全ファイル **Creative Commons 表示 4.0 国際 (CC BY 4.0)** — https://creativecommons.org/licenses/by/4.0/deed.ja

- 採用条件は「`ksj/catalog/datasets.yaml` の `license_raw` が純粋な『オープンデータ（CC_BY_4.0）』（"一部制限"・"注意事項"・「申請等必要」修飾なし）」。`CC BY 4.0（一部制限）`・年度限定の CC BY 4.0 で本文に注記があるもの・国土地理院申請が必要なもの（旧 N03 が該当）は OSS 配布で下流利用者に追加義務が発生するため不採用
- N02 は 2020 年度以降、P05 は 2022 年度、P36 は 2023 年度、P04 は 2010 年度、A38 は 2014 年度、A45 は 2019 年度、A03 は 2003 年度、N13 は 2024 年度の配布分を採用（年度によりライセンスが異なる場合があるため）

## なぜ合成フィクスチャと別に実データを持つのか

`FixtureSource.BuildRealisticShape()` の合成データは統計的に「海岸線的」な分布を模倣するが、実際の GPS トレースや測量データは:

- 量子化ノイズによる準共線点（合成では完全な滑らかカーブ）
- 歴史的な境界定義の離散ジャンプ
- 海岸線近似度の不均一（湾内は密、外洋は疎）
- 緯度経度の有効桁数が 6〜7 桁に固定

といった特徴があり、**uniform random / sinusoidal 合成**では再現しきれない。本実データはそうした現実入力に対する実装の頑健さを測る用途。`RegressionDataTests` は Point / Line / Polygon それぞれ複数ファイルを回すことで、1 データセットの癖に依存しない退行検知を担保する。

## 再生成手順

`.scratch/ksj-convert/convert.py` が KSJ ZIP → WKB 変換を担う（リポジトリ未追跡。グローバル pip 禁止のユーザールールに従い `uv run` で実行）。

```bash
# 単一ファイル
uv run .scratch/ksj-convert/convert.py A45

# 全ファイル一括
uv run .scratch/ksj-convert/convert.py
```

スクリプトの動作:

1. 各レイヤーの ZIP を `nlftp.mlit.go.jp` から `.scratch/ksj-convert/cache/` にダウンロード（既存ファイルは再利用）
2. ZIP 内部の Shapefile を `geopandas.read_file("zip://...!inner.shp")` で読む（N02 は UTF-8 サブディレクトリを指定、N13 は `_SHP.zip` URL を使う）
3. 全 features の WKB サイズを 1 MB と比較し、超過していれば stride サンプリング（`stride = ceil(全サイズ / 1MB)` で等間隔抽出、1 回で収まらなければ stride を 25% ずつ増やして再試行）
4. 採用 features を `MultiPolygon` / `MultiLineString` / `MultiPoint` に flatten し、`shapely.geometry.wkb` で LittleEndian WKB 書き出し

ベンチ用の `A45_Kagawa_NationalForest.wkt.txt` は `.scratch/ksj-convert/emit_wkt.py` が対応する `.wkb` から生成する。
