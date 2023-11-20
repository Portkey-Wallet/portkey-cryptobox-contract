using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;

namespace Portkey.Contracts.RedPacket
{
    public class RedPacketContractTestBase : DAppContractTestBase<RedPacketContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal RedPacketContractContainer.RedPacketContractStub GetRedPacketContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<RedPacketContractContainer.RedPacketContractStub>(DAppContractAddress, senderKeyPair);
        }
    }
}