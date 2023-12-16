using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Portkey.Contracts.CryptoBox
{
    public class CryptoBoxContractTestBase : DAppContractTestBase<CryptoBoxContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal CryptoBoxContractContainer.CryptoBoxContractStub CryptoBoxContractStub { get; set; }

        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }


        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        
        protected IBlockTimeProvider blockTimeProvider =>
            Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();



        protected CryptoBoxContractTestBase()
        {
            CryptoBoxContractStub = GetCryptoBoxContractStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
        }


        internal CryptoBoxContractContainer.CryptoBoxContractStub GetCryptoBoxContractStub(ECKeyPair keyPair)
        {
            return GetTester<CryptoBoxContractContainer.CryptoBoxContractStub>(DAppContractAddress,
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