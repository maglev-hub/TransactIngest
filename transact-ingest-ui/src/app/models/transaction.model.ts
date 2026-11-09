export interface TransactionModel {
  sourceSystem: string;
  transactionId: string;
  locationCode: string;
  productName: string;
  amount: number;
  transactionTime: string;
  status: string;
}
