using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Portkey.Contracts.CryptoBox
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class CryptoBoxContractState : ContractState
    {
        // state definitions go here.
        public MappedState<string, CryptoBoxInfo> CryptoBoxInfoMap { get; set; }

        public SingletonState<Address> Admin { get; set; }

        public SingletonState<bool> Initialized { get; set; }

        public MappedState<string, AddressList> AlreadySnatchedList { get; set; }

        public SingletonState<long> CryptoBoxMaxCount { get; set; }

        public SingletonState<ControllerList> TransferControllers { get; set; }
    }
}