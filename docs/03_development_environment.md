# ivy-caster 開発環境定義書

**作成日**: 2026-02-27  
**更新日**: 2026-02-27  
**バージョン**: 1.0  
**方針**: dotnet CLI + Cursor/VSCode で開発を完結（Visual Studio 必須なし）

---

## 1. 目的

- 開発者間で同一のローカル環境を再現できるようにする
- Visual Studio への依存を最小化し、CLIベースの開発フローを標準化する
- 将来的なCI/CD運用と同じ手順でローカル検証できる状態を作る

## 2. 対応OS（開発端末）

| 区分 | OS | 備考 |
| ---- | -- | ---- |
| 必須 | Windows 11 | 管理コンソール開発の主要ターゲット |
| 将来 | Linux | エージェント実装・検証を想定 |

## 3. 必須ソフトウェア

| ソフトウェア | 推奨バージョン | 用途 |
| ------------ | -------------- | ---- |
| .NET SDK | 10.0.x | ビルド、テスト、発行 |
| Git | 2.40+ | ソース管理 |
| Cursor または VSCode | 最新安定版 | コード編集 |
| PowerShell | 7.x | スクリプト実行（Windows） |

## 4. 推奨拡張（Cursor/VSCode）

- C#（ms-dotnettools.csharp）
- EditorConfig サポート
- Markdown lint

## 5. セットアップ手順（Windows）

1. リポジトリを取得する
   - `git clone <repository-url>`
   - `cd ivy-caster`
2. SDKを確認する
   - `dotnet --info`
3. 依存関係を復元する
   - `dotnet restore`
4. ビルドを実行する
   - `dotnet build -c Debug`
5. テストを実行する
   - `dotnet test -c Debug`

## 6. 開発時の標準コマンド

| 目的 | コマンド |
| ---- | -------- |
| 復元 | `dotnet restore` |
| ビルド | `dotnet build` |
| テスト | `dotnet test` |
| 発行 | `dotnet publish -c Release` |
| フォーマット | `dotnet format` |

## 7. 運用ルール

- 日常開発は `dotnet` コマンドを第一選択とする
- Visual Studio 固有機能に依存した手順は、標準手順として採用しない
- ローカル検証手順はCIと整合するコマンドで統一する

## 8. 将来拡張

- `global.json` による .NET SDK バージョン固定
- OpenSSH 経由初回展開に向けたLinux検証環境（VM/WSL）整備
- クロスプラットフォームCI（Windows + Linux）への拡張
