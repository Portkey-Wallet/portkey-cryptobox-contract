using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Portkey.Contracts.CryptoBox
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class CryptoBoxContractState : ContractState
    {
        /// <summary>
        /// CryptoBoxInfoId -> CryptoBoxInfo
        /// </summary>
        public MappedState<string, CryptoBoxInfo> CryptoBoxInfoMap { get; set; }

        /// <summary>
        /// Contracts Admin
        /// </summary>
        public SingletonState<Address> Admin { get; set; }

        /// <summary>
        /// Is contracts initialized
        /// </summary>
        public SingletonState<bool> Initialized { get; set; }

        /// <summary>
        /// CryptoBoxInfoId -> Address -> IsSnatched Map of already snatched
        /// </summary>
        public MappedState<string, Address, bool> AlreadySnatchedMap { get; set; }

        /// <summary>
        /// CryptoBoxMaxCount
        /// </summary>
        public SingletonState<long> CryptoBoxMaxCount { get; set; }
        
        /// <summary>
        /// ManagerForwardCallParallelMap
        /// </summary>
        public MappedState<Address, string, bool> ManagerForwardCallParallelMap { get; set; }
        
    }
}