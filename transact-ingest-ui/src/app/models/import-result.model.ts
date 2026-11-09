export interface ImportResultModel {
  successfulSources: number;
  failedSources: number;
  newTransactions: number;
  updatedTransactions: number;
  revokedTransactions: number;
  unchangedTransactions: number;
  finalizedTransactions: number;
}
