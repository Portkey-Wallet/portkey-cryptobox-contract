using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CryptoBox
{
    public partial class CryptoBoxContract : CryptoBoxContractContainer.CryptoBoxContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");

            // The main chain uses the audit deployment, does not verify the Author
            if (Context.ChainId != MainChainId)
            {
                AssertContractAuthor();
            }

            Assert(input != null, "Invalid input");
            if (input.Admin != null)
            {
                Assert(!input.Admin.Value.IsNullOrEmpty(), "Invalid admin address");
            }

            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.Admin.Value = input.Admin ?? Context.Sender;
            State.CryptoBoxMaxCount.Value = input.MaxCount;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Initialized.Value = true;
            return new Empty();
        }

        public override Empty CreateCryptoBox(CreateCryptoBoxInput input)
        {
            AssertContractInitialize();
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
            Assert(input.Sender != null, "SenderAddress should not be null.");
            Assert(!string.IsNullOrEmpty(input.PublicKey), "PublicKey should not be null.");
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

            var cryptoBox = new CryptoBoxInfo
            {
                CryptoBoxId = input.CryptoBoxId,
                CryptoBoxType = input.CryptoBoxType,
                CryptoBoxSymbol = input.CryptoBoxSymbol,
                TotalCount = input.TotalCount,
                TotalAmount = input.TotalAmount,
                ExpirationTime = input.ExpirationTime,
                PublicKey = input.PublicKey,
                Sender = Context.Sender
            };
            State.CryptoBoxInfoMap[input.CryptoBoxId] = cryptoBox;
            Context.Fire(
                new CryptoBoxCreated
                {
                    Sender = input.Sender,
                    CryptoBoxId = input.CryptoBoxId,
                    CryptoBoxType = input.CryptoBoxType,
                    CryptoBoxSymbol = input.CryptoBoxSymbol,
                    TotalCount = input.TotalCount,
                    TotalAmount = input.TotalAmount,
                    Receiver = virtualAddress
                }
            );
            return new Empty();
        }


        public override Empty TransferCryptoBoxes(TransferCryptoBoxesInput input)
        {
            AssertContractInitialize();
            Assert(input != null && input.TransferCryptoBoxInputs.Count > 0 && input.CryptoBoxId != null,
                "Invalidate Input");
            var cryptoBox = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(cryptoBox != null, "CryptoBox not exists.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.CryptoBoxId);
            var transferCount = 0;
            foreach (var transferCryptoBoxInput in input.TransferCryptoBoxInputs)
            {
                Assert(!transferCryptoBoxInput.Receiver.Value.IsNullOrEmpty(), "ReceiverAddress is empty");
                if (State.AlreadySnatchedMap[input.CryptoBoxId][transferCryptoBoxInput.Receiver])
                {
                    continue;
                }

                var message =
                    $"{cryptoBox.CryptoBoxId}-{transferCryptoBoxInput.Receiver}-{transferCryptoBoxInput.Amount}";
                VerifySignature(cryptoBox.PublicKey, transferCryptoBoxInput.CryptoBoxSignature,
                    message);
                State.TokenContract.Transfer.VirtualSend(virtualAddressHash, new TransferInput
                {
                    To = transferCryptoBoxInput.Receiver,
                    Amount = transferCryptoBoxInput.Amount,
                    Symbol = cryptoBox.CryptoBoxSymbol,
                    Memo = "TransferToReceiver"
                });
                Context.Fire(new CryptoBoxReceived
                {
                    CryptoBoxId = cryptoBox.CryptoBoxId,
                    Receiver = transferCryptoBoxInput.Receiver,
                    Amount = transferCryptoBoxInput.Amount,
                    Sender = cryptoBox.Sender,
                    IsSuccess = true
                });
                State.AlreadySnatchedMap[input.CryptoBoxId][transferCryptoBoxInput.Receiver] = true;
                transferCount++;
            }

            Assert(transferCount > 0, "All receivedAddress already received.");
            return new Empty();
        }

        public override Empty RefundCryptoBox(RefundCryptoBoxInput input)
        {
            AssertContractInitialize();
            Assert(!string.IsNullOrEmpty(input.CryptoBoxId), "CryptoBoxId should not be null.");
            var cryptoBox = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(cryptoBox != null, "CryptoBox not exists.");
            Assert(cryptoBox?.ExpirationTime < Context.CurrentBlockTime.Seconds * 1000, "CryptoBox not expired.");
            var virtualAddressHash = HashHelper.ComputeFrom(input.CryptoBoxId);
            var message =
                $"{cryptoBox.CryptoBoxId}-{input.Amount}";
            VerifySignature(cryptoBox.PublicKey, input.CryptoBoxSignature,
                message);
            State.TokenContract.Transfer.VirtualSend(virtualAddressHash, new TransferInput
            {
                To = cryptoBox.Sender,
                Amount = input.Amount,
                Symbol = cryptoBox.CryptoBoxSymbol,
                Memo = "RefundCryptoBox"
            });

            Context.Fire(new CryptoBoxRefunded
            {
                CryptoBoxId = cryptoBox.CryptoBoxId,
                RefundAddress = cryptoBox.Sender,
                Amount = input.Amount,
                CryptoBoxSymbol = cryptoBox.CryptoBoxSymbol
            });

            return new Empty();
        }

        public override GetCryptoBoxOutput GetCryptoBoxInfo(GetCryptoBoxInput input)
        {
            var packetInfo = State.CryptoBoxInfoMap[input.CryptoBoxId];
            Assert(packetInfo != null, "CryptoBoxId not exists.");
            return new GetCryptoBoxOutput
            {
                CryptoBoxInfo = packetInfo
            };
        }

        public override Empty SetCryptoBoxMaxCount(SetCryptoBoxMaxCountInput input)
        {
            AssertContractInitialize();
            Assert(Context.Sender == State.Admin.Value, "No permission.");
            Assert(input.MaxCount > 0, "MaxCount should be greater than 0.");
            State.CryptoBoxMaxCount.Value = input.MaxCount;
            return new Empty();
        }

        public override GetCryptoBoxMaxCountOutput GetCryptoBoxMaxCount(Empty input)
        {
            return new GetCryptoBoxMaxCountOutput
            {
                MaxCount = State.CryptoBoxMaxCount.Value
            };
        }

        private void VerifySignature(string publicKey, string signature, string message)
        {
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signatureBytes = ByteStringHelper.FromHexString(signature).ToByteArray();
            var recoverPublicKey = Context.RecoverPublicKey(signatureBytes, messageBytes).ToHex();
            Assert(recoverPublicKey == publicKey, "Invalid signature.");
        }
    }
}