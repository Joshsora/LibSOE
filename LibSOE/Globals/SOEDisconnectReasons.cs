namespace SOE
{
    public enum SOEDisconnectReasons : ushort
    {
        None,
        ICMPError,
        Timeout,
        Terminated,
        MangagerDeleted,
        ConnectFail,
        Application,
        UnreachableConnection,
        UnackTimeout,
        NewConnection,
        ConnectionRefused,
        MutualConnectError,
        ConnectingToSelf,
        ReliableOverflow
    }
}
