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
- **メッシュ変換はModelImport時（ScriptedImporter）にやっている。ランタイムでのコストはゼロ。**
- 普通のMeshRendererと同じ扱いでよい

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
