using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Portkey.Contracts.CryptoBox;

namespace Portkey.Contracts.CryptoBox
{
    public partial class CryptoBoxContract : CryptoBoxContractContainer.CryptoBoxContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.Admin.Value = input.ContractAdmin ?? Context.Sender;
            State.CryptoBoxMaxCount.Value = input.MaxCount;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Initialized.Value = true;
            return new Empty();
        }

        public override Empty CreateCryptoBox(CreateCryptoBoxInput input)
        {
            Assert(!string.IsNullOrEmpty(input.CryptoBoxId), "CryptoBoxId should not be null.");
            Assert(State.CryptoBoxInfoMap[input.CryptoBoxId] == null, "CryptoBoxId already exists.");
            Assert(input.TotalAmount > 0, "TotalAmount should be greater than 0.");
            Assert(input.TotalCount > 0, "TotalCount should be greater than 0.");
            Assert(State.CryptoBoxMaxCount.Value >= input.TotalCount,
                "TotalCount should be less than or equal to MaxCount.");
            Assert(input.MinAmount > 0, "MinAmount should be greater than 0.");
            Assert(
                !string.IsNullOrEmpty(input.CryptoBoxSymbol), "CryptoBoxSymbol should not be null.");
            Assert(input.TotalAmount >= input.MinAmount * input.TotalCount,
                "TotalAmount should be greater than MinAmount * TotalCount.");
            Assert(input.ExpirationTime > Context.CurrentBlockTime.Seconds * 1000,
                "ExpiredTime should be greater than now.");
            Assert(input.SenderAddress != null, "SenderAddress should not be null.");
            Assert(!string.IsNullOrWhiteSpace(input.CryptoBoxSignature), "signature should not be null");
            Assert(!string.IsNullOrEmpty(input.PublicKey), "PublicKey should not be null.");
            Assert(!string.IsNullOrEmpty(input.CryptoBoxSignature), "CryptoBoxSignature should not be null.");
            var maxCount = State.CryptoBoxMaxCount.Value;

            var message = $"{input.CryptoBoxId}-{input.CryptoBoxSymbol}-{input.MinAmount}-{maxCount}";
            var verifySignature = VerifySignature(input.PublicKey, input.CryptoBoxSignature, message);

            Assert(verifySignature, "Invalid signature.");
            var virtualAddress =
                Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(input.CryptoBoxId));
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = Context.Self,
                Amount = input.TotalAmount,
                Symbol = input.CryptoBoxSymbol,
                Memo = "CryptoBox"
            });

            State.TokenContract.Transfer.Send(new TransferInput
            {
                To = virtualAddress,
                Amount = input.TotalAmount,
                Symbol = input.CryptoBoxSymbol,
                Memo = "CryptoBox"
            });

            var CryptoBox = new CryptoBoxInfo
            {
                CryptoBoxId = input.CryptoBoxId,
                CryptoBoxType = input.CryptoBoxType,
                CryptoBoxSymbol = input.CryptoBoxSymbol,
                TotalCount = input.TotalCount,
                TotalAmount = input.TotalAmount,
                ExpirationTime = input.ExpirationTime,
                PublicKey = input.PublicKey,
                SenderAddress = Context.Sender
            };
            State.CryptoBoxInfoMap[input.CryptoBoxId] = CryptoBox;
            Context.Fire(
                new CryptoBoxCreated
                {
                    SenderAddress = input.SenderAddress,
                    CryptoBoxId = input.CryptoBoxId,
                    CryptoBoxType = input.CryptoBoxType,
                    CryptoBoxSymbol = input.CryptoBoxSymbol,
                    TotalCount = input.TotalCount,
                    TotalAmount = input.TotalAmount,
                    ReceiverAddress = virtualAddress
                }
            );
            return new Empty();
        }


        public override Empty TransferCryptoBox(TransferCryptoBoxBatchInput input)
        {
            var inputs = input.TransferCryptoBoxInputs;
            Assert(inputs != null && input.TransferCryptoBoxInputs.Count > 0 && input.CryptoBoxId != null,
                "Invalidate Input");
            var CryptoBox = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(CryptoBox != null, "CryptoBox not exists.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.CryptoBoxId);
            var list = State.AlreadySnatchedList[input.CryptoBoxId] ?? new AddressList();
            foreach (var transferCryptoBoxInput in inputs!)
            {
                Assert(!transferCryptoBoxInput.ReceiverAddress.Value.IsNullOrEmpty(), "ReceiverAddress is empty");
                Assert(
                    list.Addresses.FirstOrDefault(c => c.Value == transferCryptoBoxInput.ReceiverAddress.Value) == null,
                    "ReceiverAddress " + transferCryptoBoxInput.ReceiverAddress + " already receive.");

                var message =
                    $"{CryptoBox.CryptoBoxId}-{transferCryptoBoxInput.ReceiverAddress}-{transferCryptoBoxInput.Amount}";
                var verifySignature = VerifySignature(CryptoBox.PublicKey, transferCryptoBoxInput.CryptoBoxSignature,
                    message);
                Assert(verifySignature, "Signature fail:" + message);
                Context.SendVirtualInline(virtualAddressHash, State.TokenContract.Value,
                    nameof(State.TokenContract.Transfer),
                    new TransferInput
                    {
                        To = transferCryptoBoxInput.ReceiverAddress,
                        Amount = transferCryptoBoxInput.Amount,
                        Symbol = CryptoBox.CryptoBoxSymbol,
                        Memo = "TransferToReceiver"
                    }.ToByteString());
                Context.Fire(new CryptoBoxReceived
                {
                    CryptoBoxId = CryptoBox.CryptoBoxId,
                    ReceiverAddress = transferCryptoBoxInput.ReceiverAddress,
                    Amount = transferCryptoBoxInput.Amount,
                    SenderAddress = CryptoBox.SenderAddress,
                    IsSuccess = true
                });
                list.Addresses.Add(transferCryptoBoxInput.ReceiverAddress);
            }

            State.AlreadySnatchedList[input.CryptoBoxId] = list;

            return new Empty();
        }

        public override Empty RefundCryptoBox(RefundCryptoBoxInput input)
        {
            Assert(!string.IsNullOrEmpty(input.CryptoBoxId), "CryptoBoxId should not be null.");
            var CryptoBox = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(CryptoBox != null, "CryptoBox not exists.");
            Assert(CryptoBox?.ExpirationTime < Context.CurrentBlockTime.Seconds * 1000, "CryptoBox not expired.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.CryptoBoxId);
            var message =
                $"{CryptoBox.CryptoBoxId}-{input.Amount}";
            var verifySignature = VerifySignature(CryptoBox.PublicKey, input.CryptoBoxSignature,
                message);
            Assert(verifySignature, "Invalid signature.");
            Context.SendVirtualInline(virtualAddressHash, State.TokenContract.Value,
                nameof(State.TokenContract.Transfer),
                new TransferInput
                {
                    To = CryptoBox.SenderAddress,
                    Amount = input.Amount,
                    Symbol = CryptoBox.CryptoBoxSymbol,
                    Memo = "RefundCryptoBox"
                }.ToByteString());
            Context.Fire(new CryptoBoxRefunded
            {
                CryptoBoxId = CryptoBox.CryptoBoxId,
                RefundAddress = CryptoBox.SenderAddress,
                Amount = input.Amount,
                CryptoBoxSymbol = CryptoBox.CryptoBoxSymbol
            });

            return new Empty();
        }

        public override CryptoBoxOutput GetCryptoBoxInfo(GetCryptoBoxInput input)
        {
            var packetInfo = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(packetInfo != null, "CryptoBoxId not exists.");
            return new CryptoBoxOutput
            {
                CryptoBoxInfo = packetInfo
            };
        }

        public override Empty SetCryptoBoxMaxCount(SetCryptoBoxMaxCountInput input)
        {
            Assert(Context.Sender == State.Admin.Value, "No permission.");
            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.CryptoBoxMaxCount.Value = input.MaxCount;
            return new Empty();
        }

        public override CryptoBoxMaxCountOutput GetCryptoBoxMaxCount(Empty input)
        {
            return new CryptoBoxMaxCountOutput
            {
                MaxCount = State.CryptoBoxMaxCount.Value
            };
        }

        private bool VerifySignature(string publicKey, string signature, string message)
        {
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signatureBytes = ByteStringHelper.FromHexString(signature).ToByteArray();
            var recoverPublicKey = Context.RecoverPublicKey(signatureBytes, messageBytes).ToHex();
            return recoverPublicKey == publicKey;
        }
    }
}