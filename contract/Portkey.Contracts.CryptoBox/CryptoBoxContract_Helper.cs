
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

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
        
        public override Empty ChangeAdmin(AdminInput input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission");
            Assert(input != null && input.Address != null, "Invalid input");

            if (State.Admin.Value == input!.Address)
            {
                return new Empty();
            }

            State.Admin.Value = input.Address;
        
            Context.Fire(new AdminChanged
            {
                Address = input.Address
            });

            return new Empty();
        }
        
        public override AdminOutput GetAdmin(Empty input)
        {
            return new AdminOutput
            {
                Address = State.Admin.Value
            };
        }
        
    }
}