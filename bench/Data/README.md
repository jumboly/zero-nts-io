# bench/Data — 実地理データフィクスチャ

## ファイル

| ファイル | 内容 | サイズ |
|---------|------|-------|
| `N03_Kagawa.wkb` | 香川県（本島）の行政区域ポリゴン 1 件（22,418 座標、穴無し） | ~350 KB |
| `N03_Kagawa.wkt.txt` | 上と同じ形状の WKT 表現 | ~610 KB |

## 出典

**「国土数値情報（行政区域データ）N03-20240101 香川県」（国土交通省）**
https://nlftp.mlit.go.jp/ksj/gml/datalist/KsjTmplt-N03.html

ダウンロード元: https://nlftp.mlit.go.jp/ksj/gml/data/N03/N03-2024/N03-20240101_37_GML.zip

加工内容:
1. Shapefile を `NetTopologySuite.IO.ShapeFile` で読み込み
2. 全 735 ポリゴンのうち最大（22,418 座標）を 1 件抽出
3. `WKBWriter(ByteOrder.LittleEndian)` で `N03_Kagawa.wkb` に書き出し
4. `WKTWriter` で `N03_Kagawa.wkt.txt` に書き出し

変換コードは `.scratch/ksj-convert/` にある（リポジトリ未追跡）。

## ライセンス

国土数値情報利用規約: https://nlftp.mlit.go.jp/ksj/other/agreement.html

- 出典明示の上で加工・再配布・商用利用可
- 本ファイルも同規約に従い配布する
- 本ファイルを利用する派生物は同様に「国土数値情報」出典を明示すること

## なぜ合成フィクスチャと別に実データを持つのか

`FixtureSource.BuildRealisticShape()` の合成データは統計的に「海岸線的」な分布を模倣するが、実際の GPS トレースや測量データは:

- 量子化ノイズによる準共線点（合成では完全な滑らかカーブ）
- 歴史的な境界定義の離散ジャンプ
- 海岸線近似度の不均一（湾内は密、外洋は疎）
- 緯度経度の有効桁数が 6〜7 桁に固定

といった特徴があり、**uniform random / sinusoidal 合成**では再現しきれない。本実データはそうした現実入力に対する実装の頑健さを測る用途。
