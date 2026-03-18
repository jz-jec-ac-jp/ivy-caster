---
name: dotnet-named-pipes
description: Best practices for .NET Named Pipe IPC. Use when creating NamedPipeServerStream/NamedPipeClientStream, implementing pipe-based IPC, or debugging pipe deadlocks, broken pipe errors, or StreamWriter/StreamReader issues over named pipes.
---

# .NET Named Pipe IPC ガイド

## 致命的アンチパターン: BOM + AutoFlush デッドロック

`Encoding.UTF8` と `AutoFlush = true` のオブジェクト初期化子を Named Pipe で使うと**デッドロック**する。

```csharp
// ❌ DEADLOCK — 絶対にやらない
using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
```

### 原因

1. `Encoding.UTF8` は BOM プリアンブル (EF BB BF) を含む
2. `{ AutoFlush = true }` のセッターが内部で `Flush()` を呼ぶ
3. `Flush()` が BOM 3バイトをパイプに書き込み、`FlushFileBuffers` を呼ぶ
4. **サーバー側** の `FlushFileBuffers` はクライアントがデータを読むまでブロックする (Win32 仕様)
5. クライアントもまだ読み始めていないため → **デッドロック**

### 正しい書き方

```csharp
var noBom = new UTF8Encoding(false);
using var reader = new StreamReader(pipe, noBom, false, 1024, leaveOpen: true);
using var writer = new StreamWriter(pipe, noBom, 1024, leaveOpen: true);

// 書き込み後に明示的にフラッシュ
await writer.WriteLineAsync(payload);
await writer.FlushAsync(cancellationToken);
```

**ポイント:**
- `new UTF8Encoding(false)` で BOM を抑止
- `AutoFlush = true` を使わず、明示的に `FlushAsync()` を呼ぶ
- `detectEncodingFromByteOrderMarks` (StreamReader 第3引数) も `false` にする

## パイプ生成パターン

### サーバー側 (Windows ACL 付き)

```csharp
if (OperatingSystem.IsWindows())
{
    var pipeSecurity = new PipeSecurity();
    pipeSecurity.AddAccessRule(new PipeAccessRule(
        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
        PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
        AccessControlType.Allow));

    return NamedPipeServerStreamAcl.Create(
        pipeName, PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
        0, 0, pipeSecurity);
}
```

- 非昇格 Manager から昇格 Agent へ接続するには `AuthenticatedUserSid` の ACL が必要
- `PipeOptions.Asynchronous` を必ず指定（同期ハンドルだと async API がブロックする）

### クライアント側

```csharp
using var client = new NamedPipeClientStream(
    ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
await client.ConnectAsync(timeoutMs, cancellationToken);
```

## リクエスト/レスポンスプロトコル

1行 JSON + 改行のシンプルなプロトコルが堅牢:

```
Client → Server: {"action":"status","take":null}\n
Server → Client: {"success":true,"message":"OK",...}\n
```

- `WriteLineAsync` で改行付き送信 → `ReadLineAsync` で1行受信
- 1接続1リクエストが最も安全（接続を使い回さない）

## 接続ハンドラのパターン (サーバー)

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var server = CreateServerStream();
    await server.WaitForConnectionAsync(stoppingToken);
    _ = Task.Run(() => HandleSession(server, stoppingToken));
}

async Task HandleSession(NamedPipeServerStream server, CancellationToken ct)
{
    using (server)
    {
        var noBom = new UTF8Encoding(false);
        using var reader = new StreamReader(server, noBom, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(server, noBom, 1024, leaveOpen: true);

        var line = await reader.ReadLineAsync(ct);
        // ... process ...
        await writer.WriteLineAsync(response);
        await writer.FlushAsync(ct);
    }
}
```

- `using (server)` で確実に破棄
- `leaveOpen: true` で StreamReader/Writer が先にパイプを閉じるのを防ぐ
- `Task.Run` で並行セッション処理（メインループは次の接続待ちに戻る）

## よくあるエラーと対処

| エラー | 原因 | 対処 |
|--------|------|------|
| Pipe is broken | クライアントが先に切断 | stop/restart 系は成功扱いにする |
| TimeoutException on Connect | Agent 未起動 or パイプ名不一致 | パイプ名の定数を共有する |
| FlushFileBuffers ハング | BOM + AutoFlush デッドロック | `UTF8Encoding(false)` + 明示 Flush |
| Access denied | ACL 不足 | `AuthenticatedUserSid` を許可 |
