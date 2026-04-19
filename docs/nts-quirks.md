# NetTopologySuite の仕様メモ

このライブラリを実装・テストする過程で判明した **NTS 2.6 の仕様・挙動** のうち、ドキュメントに明示されていない／されにくいものをまとめる。NTS と同じバイト列・同じジオメトリを生成したい実装者向け。

---

## WKB: MultiPoint 外側タイプコードに Z/M オフセットを付けない

**ケース**: `MultiPoint` を `emitZ=true` 付きで書き出すとき、**外側のタイプコードは 4 のまま**（1004 にならない）。一方で `MultiLineString` / `MultiPolygon` / `GeometryCollection` は **1005 / 1006 / 1007**（Z オフセット付き）で書き出される。

**確認した出力例** (`NetTopologySuite` 2.6, `WKBWriter(LE, emitZ: true)`):

| 入力 | 外側 type code | 内側子 type code |
|------|----------------|------------------|
| `MultiPoint XYZ` | **4**（オフセット無し） | 1001（Point Z） |
| `MultiLineString XYZ` | 1005 | 1002 |
| `MultiPolygon XYZ` | 1006 | 1003 |
| `GeometryCollection XYZ` | 1007 | 各子の type |

M / ZM でも同様に MultiPoint だけ外側は 4。

**理由（推測）**: OGC SFS 1.1（SFSQL）の MultiPoint 規定をそのまま踏襲していると思われる。SFS 1.1 は 2D 前提で、3D 拡張時に MultiPoint だけ従来形式を残した歴史的経緯。

**影響**: OGC ISO WKB と言いつつ MultiPoint だけ SFS 1.1 形式を混在させる挙動のため、互換実装を書くときは **MultiPoint だけ特別扱い** が必要。

**対応箇所**: `src/ZeroNtsIo/ZWkbWriter.cs` の `suppressOrdOffset = g is MultiPoint`。

---

## `NtsGeometryServices.Instance` の既定は Z/M を落とす

`CoordinateArraySequenceFactory`（既定）は 2D 前提で、`WKBReader` や `WKTReader` に渡すと **Z/M 座標が round-trip で黙って消える**ことがある。

**再現手順**:
```csharp
var services = NtsGeometryServices.Instance;     // 既定 = CoordinateArraySequenceFactory
var reader = new WKBReader(services);
var g = reader.Read(wkbWithZ);                    // Z が失われることがある
```

**対応**:
```csharp
var services = new NtsGeometryServices(
    PackedCoordinateSequenceFactory.DoubleFactory);   // dim 自動対応
var reader = new WKBReader(services);
```

`PackedCoordinateSequenceFactory.DoubleFactory` は **static な既定インスタンス**（`Dimension=3, Measures=0`）で、`WKBReader` / `WKTReader` が読み取ったジオメトリの次元情報に合わせて `Create(size, dim, measures)` を呼ぶため、Z/M が保持される。

---

## `PackedCoordinateSequenceFactory.Create(double[], int, int)` は所有権を受け取る

```csharp
// 配列をコピーせずに受け取る（ゼロコピー）
var seq = packedFactory.Create(rawCoords, dimension: 3, measures: 0);
```

**要件**:
- 配列長は厳密に `count * dimension` 要素
- 座標レイアウトは `[x0, y0, z0?, m0?, x1, y1, ...]`（次元分だけ詰めて連続）
- 渡した後に配列を変更してはならない（NTS は内部で保持する）

**使い所**: WKB-LE のバイト列は OGC 仕様上この `double[]` レイアウトと**バイト単位で一致する**（LE マシン前提）。`MemoryMarshal.Cast<byte, double>(span).CopyTo(arr)` 1 回でパース完了し、そのまま `Create()` に渡せばジオメトリが完成する。

**ArrayPool との併用不可**: 所有権を取られるので、ジオメトリ寿命中に `ArrayPool.Return` できない。Pool を使いたいなら**スクラッチバッファ**に限定し、最終座標配列は通常の `new double[]` で確保する。

---

## `WKBWriter` の `emitZ` / `emitM` フラグと実データの不整合

`new WKBWriter(byteOrder, handleSRID, emitZ, emitM)` のフラグは実データと**整合している必要がある**。ズレると：

| 状況 | 結果 |
|------|------|
| データに Z あり、`emitZ=false` | Z が黙って出力から消える（**データ欠損**） |
| データに Z なし、`emitZ=true` | 存在しない Z 座標が書かれようとしてエラー or 不定 |
| データ XYZM、`emitZ=true, emitM=false` | M が消える |

**正しい判定方法**:
```csharp
bool hasZ = /* ジオメトリの全 CoordinateSequence を走査し seq.HasZ の OR を取る */;
bool hasM = /* 同様に seq.HasM */;
var writer = new WKBWriter(byteOrder, handleSRID: false, emitZ: hasZ, emitM: hasM);
```

**注意**: Multi\*/GeometryCollection の自分自身 (`MultiPoint.HasZ` 等) は**あてにならない**。子ジオメトリの `CoordinateSequence.HasZ` を再帰的に見る必要がある。この判定を `g.NumGeometries > 1` で分岐すると、子が 1 個しか無い Multi\* で Z/M を落とすバグになる（`is MultiPoint or MultiLineString or ...` で型判定するのが正解）。

**対応箇所**: `src/ZeroNtsIo.Reference/NtsWkbWriter.cs` の `AnySeq`。

---

## OGC WKB POINT EMPTY は全座標 NaN

`POINT EMPTY` を WKB にすると、次元分の `double.NaN` が座標として書き込まれる:

```
01 01 00 00 00 00 00 00 00 00 00 F8 7F 00 00 00 00 00 00 F8 7F
│  │           │                       │
│  Point       X = NaN                  Y = NaN
byte order
```

PostGIS は別形式（空の MultiPoint で表現）を使うこともあるが、**NTS は NaN 全書き**方式。読み取り側は「X と Y が両方 NaN なら `CreatePoint(null)` で空 Point を返す」扱いで一致させる。

---

## WKTWriter の数値フォーマットは `"R"`

NTS 2.x は double を **`"R"` (round-trip) format** で書き出す。`"G17"` ではない。多くの場合結果は同じだが、境界値（`double.MaxValue` 付近）でフォーマットが微妙に異なる。

**比較テストでは WKT 文字列を直接比較せず**、parse して座標をビット単位で比較するのが安全。

---

## WKT での NaN / Infinity リテラル

NTS は NaN 値を **`NaN`** と書き出す（大文字小文字ミックス）。Infinity は **`Infinity`** / **`-Infinity`**。

`double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _)` は **`NumberStyles.Float` だけで** "NaN" / "Infinity" を受け付ける（`AllowSpecialValues` フラグは不要）。

逆に自作パーサが `[+-]?\d+` だけを消費する loop だと letter-started トークンを取り損ねる。数値トークン抽出は `IsLetter` 始まりも拾う実装にする必要がある。

---

## WKT の大文字小文字・空白

NTS の `WKTReader` は以下を全て受理する:

```
POINT(1 2)
POINT (1 2)
point(1 2)
Point(1 2)
POINT  (  1   2  )
POINT\t(1\t2)
POINT\n(1\n2)
POINT\r\n(1\r\n2)
```

**互換実装では `char.IsWhiteSpace` を使って `\t \r \n` も空白として扱う**。

---

## MultiPoint WKT の両形式

MultiPoint WKT は 2 つの書式が通る:

```
MULTIPOINT (1 2, 3 4)         -- 外側カッコのみ
MULTIPOINT ((1 2), (3 4))     -- 各 Point を内側カッコで包む
```

NTS は両方受理する。互換実装も両形式に対応する必要がある。NTS の `WKTWriter` は `((1 2), (3 4))` 形式で出力する。

---

## `PackedDoubleCoordinateSequence` の内部レイアウト

```csharp
// 内部 double[] のインデックス
var raw = seq.GetRawCoordinates();     // NTS 2.x で public
// for XYZM (dim=4, measures=1):
// raw[i*4+0] = X[i], raw[i*4+1] = Y[i], raw[i*4+2] = Z[i], raw[i*4+3] = M[i]
// for XYM (dim=3, measures=1):
// raw[i*3+0] = X[i], raw[i*3+1] = Y[i], raw[i*3+2] = M[i]        ← Z スロットが M
```

**重要**: XYM では M が配列の 3 番目のスロット（通常 Z の位置）に入る。WKB にシリアライズするときもこの順（X, Y, M）で書く。

---

## `Coordinate` 派生型とファクトリの相互作用

| コード | 作られる sequence | HasZ | HasM |
|--------|------------------|------|------|
| `factory.CreatePoint(new Coordinate(x, y))` | XY (dim=2) | false | false |
| `factory.CreatePoint(new CoordinateZ(x, y, z))` | XYZ (dim=3) | true | false |
| `factory.CreatePoint(new CoordinateM(x, y, m))` | XYM (dim=3, measures=1) | false | true |
| `factory.CreatePoint(new CoordinateZM(x, y, z, m))` | XYZM (dim=4, measures=1) | true | true |

これは `PackedCoordinateSequenceFactory.Create(Coordinate[])` が coord の型を検査して dim/measures を決めるため。**ただし** `Coordinate[]` に**型が混在**していると、最初の要素（or 最大 dim）に合わせられて他の値が落ちることがある — テストフィクスチャを作るときは配列内で型を揃える。

---

## EWKB (PostGIS) の識別

OGC ISO WKB と EWKB は一見同じヘッダだが、EWKB は型コードの上位ビットに拡張フラグを立てる:

| ビット | 意味 | マスク |
|--------|------|--------|
| 0x20000000 | SRID 埋め込み | `(type & 0x20000000) != 0` |
| 0x40000000 | M dimension (旧 PostGIS) | `(type & 0x40000000) != 0` |
| 0x80000000 | Z dimension (旧 PostGIS) | `(type & 0x80000000) != 0` |

NTS は EWKB も部分的に解釈するが、このライブラリは **OGC ISO 専用**で `(rawType & 0xE0000000) != 0` なら即座に `FormatException` を投げる方針。SRID を黙って無視すると別地物として扱うバグになり得るため。

---

## まとめ: NTS 互換 WKT/WKB を書くときのチェックリスト

1. `PackedCoordinateSequenceFactory.DoubleFactory` ベースの `NtsGeometryServices` を使う
2. `WKBWriter` の `emitZ` / `emitM` はジオメトリ実データから再帰的に判定する（NumGeometries カウントに頼らない）
3. MultiPoint 外側 WKB タイプコードは Z/M オフセット**無し**で書く（他の Multi*/GC は付ける）
4. `POINT EMPTY` は WKB で全ordinates NaN として書く
5. WKT 数値パーサは `NaN` / `Infinity` / `-Infinity` リテラルを受け取れるようにする
6. WKT トークン分割で `\t \r \n` も空白として扱う
7. MultiPoint WKT の 2 種類の書式 `(1 2, 3 4)` / `((1 2), (3 4))` の両方を受理する
8. EWKB フラグ（0xE0000000）が立っていたら明示的に拒否する
