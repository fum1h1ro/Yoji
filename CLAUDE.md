# Bee

## プロジェクト概要

Unityで自作ScriptableRenderPipelineを使ったゲーム。
ワイヤーフレーム表現＋アキュムレーション型モーションブラーが特徴。
シミュレーションにDOTS/ECSを使用。

## アーキテクチャ

### レンダリング

- **パイプライン**: 自作 `ScriptableRenderPipeline`（URPもHDRPも使わない）
- **描画方式**: `Graphics.DrawMeshInstanced` でECSのTransformを毎フレーム渡す
- **Entities Graphicsは使用しない**

### ワイヤーフレームメッシュ

- エッジ1本 = 細長い矩形（2 Triangle）
- プリミティブは `TRIANGLES`（LINEは使わない）
- 頂点は位置＋エッジ両側の面法線2本を持つ。線を描画するか・太さをどうするかの判定は全て `YojiSimple.shader` 側で行う。C#/Job側は正しい位置と法線2本を渡すことに専念する
- **非SkinnedMesh**: ModelImport時（`Importer.cs`のScriptedImporter）に法線まで確定させ`VertexBuffer`に直接焼き込む。ランタイムコストはゼロ。普通のMeshRendererのまま
- **SkinnedMesh**: バインドポーズでは最終形状が決まらないため、ModelImport時は4頂点（Begin/End/Left/Right）とBoneWeightを`FrameStructure`(ScriptableObject)に保持するだけに留め、法線計算はランタイムの`FrameRenderer`のJobで行う（4ボーンLBS→毎フレーム2面法線を再計算）。`SkinnedMeshRenderer`は`FrameRenderer`に差し替えられる
- `DestroyUselessWire`（隣接三角形の法線が同じならワイヤーを削除する最適化）はバインドポーズ時点の判定なので**SkinnedMeshには使えない**（変形後に必要な線が消える）。`ConvertSkinnedMesh`では常に無効化する
- 罠: `TriangleEdge.Next.First` は `edge.Second` と数学的に恒等になる。三角形の3番目の頂点は `edge.Second.Next` で取ること。隣接三角形は共有辺の巻きが逆なので、隣接側の法線は基準点をend側にして計算する
- 罠: `YojiImporter.GetVersion()` が固定値のため、インポーターのコードを変えても既存の.fbxは自動再インポートされない。変換ロジックを直したら対象アセットを強制再インポートする

### アキュムレーション型モーションブラー

物理的に正確な露光積算方式。ポストプロセスのベロシティブラーではない。

```
1フレーム = シャッター開放期間
  ├── サブステップ1: ECSシミュ更新 → RenderTextureに描画
  ├── サブステップ2: ECSシミュ更新 → RenderTextureに描画
  ├── ...
  └── N枚を平均して最終出力
```

- サブステップ数: デフォルト4（調整可能）
- ループはSRPの `Render()` 内で完全制御
- 各サブステップ後にECSのJobをCompleteしてmatrix配列を取得し描画

### カラースペース・HDR

- **カラースペース**: Linear。頂点カラー（`Color32`、sRGB想定の生バイト）はUnityが自動変換しないため、`YojiSimple.shader`側で明示的にGamma→Linear変換する。線の濃度パラメータ（`_BackLineDensity`等）は`[Gamma]`を付け、Gammaで調整した見た目のまま自動変換に任せる
- **シーンRT**: `GraphicsFormat.R16G16B16A16_SFloat`。UNORMは加算が1.0でクランプされ、`R11G11B10`は仮数が6/5bitしかなく明るい所に重ねた薄い線が消える
- 全て加算合成なので、float RTなら各サブステップを1枚のRTへ1/N倍で加算描画すれば、N枚平均のアキュムレーションと等価になる（`YojiBlurComposit`は不要になる見込み）
- **最終パス**: `Hidden/Yoji/Final`（`YojiFinal.shader`）をフルスクリーン三角形（`DrawProcedural`、3頂点、メッシュ不要）で描き、シーンRTをバックバッファへ書く
  - `HDROutputSettings.main.active`のとき: `HDROutputUtils.ConfigureHDROutput(mat, HDROutputSettings.main.displayColorGamut, Operation.ColorConversion | Operation.ColorEncoding)`を呼び、`_PaperWhite`/`_MinNits`/`_MaxNits`に`paperWhiteNits`/`minToneMapLuminance`/`maxToneMapLuminance`を渡す。Unity側の自動トーンマップと二重にならないよう`automaticHDRTonemapping = false`にする
  - SDR時はキーワードを無効にしたまま使う
  - HDR表示への切り替えは`HDROutputSettings.main.RequestHDRModeChange(true)`で行う（`PlayerSettings.useHDRDisplay`はオフのまま。起動時に自動でHDRへ切り替えない）
  - `HDROutputUtils`を使うasmdefには`Unity.RenderPipelines.Core.Runtime`の参照が必要
  - HDR出力の実機確認はEDR対応のMacか有機ELのiPhoneで行う。**iPhone SE2はHDR表示非対応**（True Tone Retina HD液晶、625 nits）なのでSDRフォールバックの動作確認のみ
- `YojiBloom.shader`はLDR/Gamma時代のまま放置している。使う前に直す必要がある問題：alpha重みのGaussがfloat RTで段ごとに指数的に増幅する、彩度係数`1-a`がa>1で負になり`pow`でNaNになる、`pow(2.2)`のガンマカーブがLinearでは二重補正になる
- エディタ（desktop Metal）は`half`=`float`なので、halfのオーバーフロー（`YojiBloom`の問題など）は実機でしか再現しない

### ECS（DOTS）

- シミュレーション（物理・ゲームロジック）のみECSで管理
- Transformデータをレンダリング側に渡す責務も持つ
- サブステップごとに `world.Update()` を呼ぶ

## SRP実装の基本構造

```csharp
public class WireframeRenderPipeline : RenderPipeline
{
    protected override void Render(ScriptableRenderContext ctx, Camera[] cameras)
    {
        for (int i = 0; i < subStepCount; i++)
        {
            simWorld.Update();                          // ECSサブステップ
            // JobComplete → matrix[] 取得
            Graphics.DrawMeshInstanced(...);            // ワイヤーフレームメッシュ描画
            // RenderTextureにAccumulate
        }
        // 最終合成してスクリーンにBlit
    }
}
```

## パフォーマンス目標

- **ターゲット**: 60fps / iPhone SE2（A13 Bionic）
- フラグメントシェーダはほぼ処理なし（ボトルネックはシミュレーション側）
- サブステップ4でiPhone SE2で実機検証済み

## 制約・方針

- Unity 6（6000.x）使用
- URPのRenderGraph等のAPIは使わない
- Unityのレンダリング機能への依存を最小限にする
- シェーダーはシンプルなUnlit
- エディタ系機能（Gizmos等）はゲームビューに閉じるので考慮不要
