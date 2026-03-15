namespace IvyCaster.Agent.Manager.Messages;

public static class TroubleshootingMessages
{
    public const string ElevationRejected = "管理者権限への昇格が拒否されたため、操作を中止しました。";
    public const string ElevationFailed = "管理者権限への昇格に失敗しました。管理者として再試行してください。";
    public const string OperationFailedPrefix = "操作に失敗しました:";
    public const string OperationSucceededPrefix = "操作が完了しました:";
    public const string AgentNotReachable = "Agentに接続できません。サービス起動状態を確認してください。";
}
