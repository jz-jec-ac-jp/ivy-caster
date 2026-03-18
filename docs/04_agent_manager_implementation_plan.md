# ivy-caster Agent/Manager 実装計画書

**作成日**: 2026-03-05  
**更新日**: 2026-03-05  
**バージョン**: 1.0  
**対象**: `IvyCaster.Agent` / `IvyCaster.Agent.Manager`（Avalonia）

---

## 1. 目的

- エージェント本体（サービス/デーモン）と管理フロント（UI）を分離し、責務を明確化する
- Windows 先行で動作させつつ、Linux/macOS への拡張余地を確保する
- トラブルシューティング時に、管理者が最小操作で状態確認・停止・再起動できる導線を用意する

## 2. 実装方針

### 2.1 構成分離

- `IvyCaster.Agent`  
  - Worker Service として常駐
  - 管理 API（ローカル IPC）をホスト
  - OS依存処理は抽象インターフェース経由で実行
- `IvyCaster.Agent.Manager`  
  - Avalonia デスクトップアプリ
  - 起動時にタスクトレイ常駐
  - 管理 API クライアントとして Agent を制御

### 2.2 HAL相当の抽象化

- `IvyCaster.Core` に抽象契約を定義する
  - `IAgentServiceController`
  - `IAgentLogProvider`
  - `IAgentProcessInspector`
  - `IPrivilegeElevationService`
- `IvyCaster.Agent/Platform` に OS 実装を分離する
  - `Windows/*` 実装
  - `Linux/*` 実装（初期はスタブ可）
- アプリケーション層は抽象契約にのみ依存する

### 2.3 管理 API（IPC）

- 方式: Named Pipe（ローカル）
- 主な操作:
  - `status`
  - `logs`
  - `stop`
  - `restart`
- 契約:
  - `AgentManagementRequest`
  - `AgentManagementResponse`
  - `AgentStatusSnapshot`
  - `AgentLogEntry`

### 2.4 権限モデル

- `Manager` は通常権限で常駐する
- 破壊的操作（停止/再起動）時のみ昇格導線を使う
  - Windows: UAC（`runas`）
  - Linux/macOS: 将来 `sudo` 系へ差し替え可能な構造
- 昇格拒否時は操作を中止し、メッセージのみ表示する

## 3. 実装ステップ

1. **基盤整備**
   - `IvyCaster.Core` に管理 API / 抽象契約を追加
2. **Agent 実装**
   - 管理サービスと Pipe サーバーを追加
   - Runtime 状態/ログ保持を追加
   - OS 切替 DI を追加
3. **Manager 実装**
   - Avalonia プロジェクト作成
   - API クライアント実装
   - メイン画面（状態/ログ/停止/再起動）実装
   - トレイ常駐導線と閉じる時の非終了化
4. **権限・メッセージ**
   - 操作時昇格
   - 昇格拒否/失敗メッセージ
5. **検証・調整**
   - ビルド、手動動作確認、既知課題の整理

## 4. 成果物（想定）

- `src/IvyCaster.Core/AgentContracts.cs` の拡張
- `src/IvyCaster.Agent/Management/*`
- `src/IvyCaster.Agent/Runtime/*`
- `src/IvyCaster.Agent/Platform/Windows/*`
- `src/IvyCaster.Agent/Platform/Linux/*`
- `src/IvyCaster.Agent.Manager/*`

## 5. リスクと対応

- **トレイ初期化失敗**: 例外安全化し、失敗時は通常ウィンドウ表示へフォールバック
- **実行ファイルロック**: 実行中プロセスがある場合は再ビルド前に停止
- **OS差分増加**: 抽象契約と Platform 実装を分離し、差分を局所化
