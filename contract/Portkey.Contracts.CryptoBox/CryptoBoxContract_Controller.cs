using System.Collections.Generic;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CryptoBox
{
    public partial class CryptoBoxContract : CryptoBoxContractContainer.CryptoBoxContractBase
    {
        public override Empty AddTransferController(ControllerInput input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission");
            Assert(input != null && input.Address != null, "Invalid input");

            var controller = State.TransferControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
            if (controller != null)
            {
                return new Empty();
            }

            State.TransferControllers.Value.Controllers.Add(input!.Address);
            Context.Fire(new TransferControllerAdded
            {
                Address = input.Address
            });

            return new Empty();
        }

        public override Empty RemoveTransferController(ControllerInput input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission");
            Assert(input != null && input.Address != null, "Invalid input");

            var controller = State.TransferControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
            if (controller == null)
            {
                return new Empty();
            }

            State.TransferControllers.Value.Controllers.Remove(controller);

            Context.Fire(new TransferControllerRemoved
            {
                Address = input!.Address
            });

            return new Empty();
        }

        public override ControllerOutput GetTransferControllers(Empty input)
        {
            return new ControllerOutput
            {
                Addresses = { State.TransferControllers.Value.Controllers }
            };
        }
    }
}