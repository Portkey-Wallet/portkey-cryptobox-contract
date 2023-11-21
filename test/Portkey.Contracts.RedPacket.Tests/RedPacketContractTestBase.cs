using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace Portkey.Contracts.RedPacket
{
    public class RedPacketContractTestBase : DAppContractTestBase<RedPacketContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal RedPacketContractContainer.RedPacketContractStub RedPacketContractStub { get; set; }

        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;

        protected Address RedContractAddress { get; set; }

        internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }


        protected RedPacketContractTestBase()
        {
            ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
            var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Category = KernelConstants.CodeCoverageRunnerCategory,
                    Code = ByteString.CopyFrom(
                        File.ReadAllBytes(typeof(RedPacketContract).Assembly.Location))
                }));
            RedContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);

            RedPacketContractStub = GetRedPacketContractStub(DefaultKeyPair);
        }

        internal RedPacketContractContainer.RedPacketContractStub GetRedPacketContractStub(ECKeyPair keyPair)
        {
            return GetTester<RedPacketContractContainer.RedPacketContractStub>(RedContractAddress,
                keyPair);
        }


        internal ACS0Container.ACS0Stub GetContractZeroTester(
            ECKeyPair keyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
                keyPair);
        }
    }
}