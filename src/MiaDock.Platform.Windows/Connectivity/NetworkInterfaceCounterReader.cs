using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Connectivity;

internal static class NetworkInterfaceCounterReader
{
    private const int IfMaxStringSize = 256;
    private const int IfMaxPhysAddressLength = 32;

    public static bool TryRead(Guid interfaceId, out ulong receivedBytes, out ulong sentBytes)
    {
        receivedBytes = 0;
        sentBytes = 0;
        if (ConvertInterfaceGuidToLuid(ref interfaceId, out var luid) != 0) return false;

        var row = MibIfRow2.Create(luid);
        if (GetIfEntry2(ref row) != 0) return false;
        receivedBytes = row.InOctets;
        sentBytes = row.OutOctets;
        return true;
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint ConvertInterfaceGuidToLuid(ref Guid interfaceGuid, out ulong interfaceLuid);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetIfEntry2(ref MibIfRow2 row);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MibIfRow2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)] public string Alias;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)] public string Description;
        public uint PhysicalAddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)] public byte[] PhysicalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)] public byte[] PermanentPhysicalAddress;
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;

        public static MibIfRow2 Create(ulong luid) => new()
        {
            InterfaceLuid = luid,
            Alias = string.Empty,
            Description = string.Empty,
            PhysicalAddress = new byte[IfMaxPhysAddressLength],
            PermanentPhysicalAddress = new byte[IfMaxPhysAddressLength]
        };
    }
}
