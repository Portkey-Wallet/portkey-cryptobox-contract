using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Threading;

namespace Portkey.Contracts.RedPacket
{
    public class RedPacketContractTestBase : DAppContractTestBase<RedPacketContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal RedPacketContractContainer.RedPacketContractStub RedPacketContractStub { get; set; }

        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }


        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        
        protected IBlockTimeProvider blockTimeProvider =>
            Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();



        protected RedPacketContractTestBase()
        {
            RedPacketContractStub = GetRedPacketContractStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
        }


        internal RedPacketContractContainer.RedPacketContractStub GetRedPacketContractStub(ECKeyPair keyPair)
        {
            return GetTester<RedPacketContractContainer.RedPacketContractStub>(DAppContractAddress,
                keyPair);
        }

        internal TokenContractContainer.TokenContractStub GetTokenContractStub(
            ECKeyPair keyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress,
                keyPair);
        }
    }
}