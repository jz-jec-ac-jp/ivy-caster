# ivy-caster Agent/Manager テスト方法書

**作成日**: 2026-03-05  
**更新日**: 2026-03-05  
**バージョン**: 1.0  
**対象**: `IvyCaster.Agent` / `IvyCaster.Agent.Manager`（Avalonia）

---

## 1. 目的

- Agent/Manager 分離構成が要件どおり動作することを確認する
- トレイ常駐、管理操作、昇格導線、ログ確認の主要導線を検証する
- 将来の回帰確認に使える手順を標準化する

## 2. 事前準備

1. .NET SDK 10.0.x を確認  
   - `dotnet --info`
2. 依存復元  
   - `dotnet restore`
3. 実行中の `IvyCaster.Agent.Manager` を終了  
   - ビルド時のファイルロック回避

## 3. ビルド確認

### 3.1 Agent

- `dotnet build src/IvyCaster.Agent/IvyCaster.Agent.csproj -c Debug`
- 期待結果:
  - `0 error`

### 3.2 Manager

- `dotnet build src/IvyCaster.Agent.Manager/IvyCaster.Agent.Manager.csproj -c Debug`
- `exe` ロック時の代替:
  - `dotnet build src/IvyCaster.Agent.Manager/IvyCaster.Agent.Manager.csproj -c Debug -p:UseAppHost=false`
- 期待結果:
  - `0 error`

## 4. 手動テスト項目

## 4.1 起動・常駐

1. Agent を起動
2. Manager を起動
3. 期待結果
   - Manager は起動後にトレイ常駐する
   - ウィンドウを閉じても終了せず常駐する
   - トレイメニューから再表示できる

## 4.2 状態確認

1. Manager の「状態更新」を実行
2. 期待結果
   - `AgentId`, `Host`, `State`, `ProcessId`, `LastHeartbeat` が表示される
   - Agent 未起動時は接続不可メッセージが表示される

## 4.3 ログ確認

1. Manager の「ログ更新」を実行
2. 期待結果
   - 直近ログが表示される
   - 取得失敗時は失敗メッセージが表示される

## 4.4 停止/再起動（昇格）

1. 「停止」または「再起動」を実行
2. 期待結果
   - 操作時に昇格（Windows: UAC）が要求される
   - 許可時は操作成功メッセージ
   - 拒否時は「昇格拒否」メッセージのみ表示し、安全に中止

## 5. 異常系テスト

- Agent 未起動で Manager から操作
  - 期待: 接続不可メッセージ
- Pipe 名不一致/通信失敗（開発用に一時変更）
  - 期待: 操作失敗メッセージ
- 昇格拒否
  - 期待: 状態維持、操作未実行

## 6. 回帰確認チェックリスト

- [ ] Agent が単体起動できる
- [ ] Manager が単体起動できる
- [ ] トレイ常駐が維持される
- [ ] 状態表示が更新される
- [ ] ログ取得が更新される
- [ ] 停止操作が動作する
- [ ] 再起動操作が動作する
- [ ] 昇格拒否メッセージが表示される

## 7. 既知の注意点

- 実行中の Manager があると `exe`/`dll` ロックでビルド警告または失敗が出ることがある
- ロック時はプロセス停止後に再ビルドする
- CI/自動化時は `UseAppHost=false` の利用を検討する
