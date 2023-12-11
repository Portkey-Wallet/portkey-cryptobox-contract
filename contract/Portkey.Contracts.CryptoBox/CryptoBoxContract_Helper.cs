using System.Collections.Generic;
using AElf.Contracts.MultiToken;
using AElf.Types;

namespace Portkey.Contracts.CryptoBox
{
    public partial class CryptoBoxContract : CryptoBoxContractContainer.CryptoBoxContractBase
    {
        private void AssertContractInitialize()
        {
            Assert(State.Initialized.Value, "Contract not Initialized.");
        }

        private void AssertContractAuthor()
        {
            // Initialize by author only
            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            var author = State.GenesisContract.GetContractAuthor.Call(Context.Self);
            Assert(author == Context.Sender, "No permission");
        }
    }
}