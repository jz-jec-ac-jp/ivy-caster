# ivy-caster アーキテクチャ設計書

**作成日**: 2026-02-21  
**更新日**: 2026-02-21  
**バージョン**: 2.0  
**技術スタック**: WPF (.NET 10) + C# 統一

---

## 1. 技術スタック

| レイヤー                 | 技術                                     | 選定理由                                                  |
| ------------------------ | ---------------------------------------- | --------------------------------------------------------- |
| 管理コンソール           | WPF (.NET 10) + C#                       | Windowsネイティブ、TreeView/ListView/ContextMenu 標準装備 |
| アーキテクチャ           | MVVM                                     | WPF標準、テスタビリティ、関心の分離                       |
| クライアントエージェント | .NET 10 Worker Service + C#              | OS依存処理を抽象化し、将来Linux対応を可能にする           |
| 通信                     | gRPC over TLS (Grpc.Net)                 | 双方向通信、ストリーミング、.NETネイティブ                |
| データベース             | SQLite + Entity Framework Core           | インストール不要、Code First、LINQ                        |
| DI                       | Microsoft.Extensions.DependencyInjection | .NET標準                                                  |
| ログ                     | Serilog                                  | 構造化ログ                                                |
| インストーラー           | WiX Toolset                              | MSI作成                                                   |

## 2. システム構成図

```text
管理者PC (Windows 11)
└── Ivy-Caster Console (.exe / WPF .NET 10)
    ├── View層 (XAML)      ← TreeView + ListView + ContextMenu
    ├── ViewModel層 (C#)   ← MVVM、ICommand、ObservableCollection
    ├── Service層 (C#)     ← ビジネスロジック
    ├── Infrastructure層   ← EF Core + gRPC Server
    └── SQLite DB           ← jz-ghost.db

学生PC × N台
└── Ivy-Caster Agent (.exe / .NET 10 Worker Service)
    ├── gRPC Client        ← サーバー接続・ハートビート
    ├── CommandExecutor     ← OS抽象化経由でコマンド実行
    ├── PlatformAbstractions← IProcessRunner / IFileSystem / IServiceController
    ├── FileReceiver        ← ファイル受信・チェックサム検証
    └── ResultReporter      ← 結果送信
```

## 3. 通信設計

| 用途     | ポート | プロトコル |
| -------- | ------ | ---------- |
| gRPC通信 | 50051  | TCP/TLS    |

### 複数セグメント対応

- **方式A**: 直接通信（ルーティング可能な場合）
- **方式B**: リレーエージェント（ルーティング不可の場合）

### 初回展開チャネル（エージェント）

- 初回展開チャネルは `IDeploymentChannel` で抽象化する
- 現段階の必須実装は `WmiDeploymentChannel` とする
- アプリケーション層は `IDeploymentChannel` のみに依存し、特定プロトコルへ直接依存しない
- 将来的に `SshDeploymentChannel`（OpenSSH）を追加可能な構造とする

### OS抽象化方針（エージェント）

- OS依存処理（プロセス実行、サービス操作、パス処理、シェル呼び出し）はインターフェース化する
- 例: `IProcessRunner`, `IFileSystem`, `IServiceController`, `IShellAdapter`
- Windows実装（PowerShell/cmd）とLinux実装（bash/systemd）を分離し、DIで切り替える
- アプリケーション層は抽象インターフェースにのみ依存し、OS固有APIへ直接依存しない

## 4. .NET ソリューション構造

```text
jz-ghost/
├── IvyCaster.slnx
├── src/
│   ├── IvyCaster.Console/       # WPF管理コンソール
│   │   ├── Views/               # XAML
│   │   ├── ViewModels/          # ViewModel (MVVM)
│   │   ├── Controls/            # カスタムコントロール
│   │   └── Converters/          # 値コンバーター
│   ├── IvyCaster.Agent/         # Worker Serviceエージェント
│   ├── IvyCaster.Core/          # ドメインモデル・インターフェース
│   │   ├── Models/              # PcInfo, PcGroup, TaskDefinition
│   │   ├── Enums/               # PcStatus, TaskType
│   │   └── Interfaces/          # IPcManagementService等
│   ├── IvyCaster.Infrastructure/ # EF Core + gRPC実装
│   └── IvyCaster.Shared/        # Proto定義・共通DTO
├── tests/
├── docs/
└── README.md
```

## 5. 主要NuGetパッケージ

| パッケージ                             | 用途                                   |
| -------------------------------------- | -------------------------------------- |
| `Grpc.AspNetCore`                      | gRPCサーバー                           |
| `Grpc.Net.Client`                      | gRPCクライアント                       |
| `Google.Protobuf`                      | Protocol Buffers                       |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLiteアクセス                         |
| `CommunityToolkit.Mvvm`                | MVVM基盤（ICommand, ObservableObject） |
| `Serilog.Sinks.File`                   | ファイルログ                           |
| `Microsoft.Extensions.Hosting`         | Worker Service ホスト                  |
