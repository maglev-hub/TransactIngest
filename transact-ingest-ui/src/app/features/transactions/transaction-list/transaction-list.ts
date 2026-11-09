import { Component, OnInit, computed, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Transaction } from '../../../services/transaction';
import { TransactionModel } from '../../../models/transaction.model';
import { ImportResultModel } from '../../../models/import-result.model';

type SortColumn =
  | 'sourceSystem'
  | 'transactionId'
  | 'locationCode'
  | 'productName'
  | 'amount'
  | 'transactionTime'
  | 'status';

@Component({
  selector: 'app-transaction-list',
  standalone: true,
  imports: [CurrencyPipe],
  templateUrl: './transaction-list.html',
  styleUrl: './transaction-list.css'
})
export class TransactionList implements OnInit {

  transactions = signal<TransactionModel[]>([]);

  importResult = signal<ImportResultModel | null>(null);

  isImporting = signal(false);

  errorMessage = signal<string | null>(null);

  loadError = signal<string | null>(null);

  isLoading = signal(false);

  sortColumn = signal<SortColumn>('transactionTime');

  sortDirection = signal<'asc' | 'desc'>('desc');

  sortedTransactions = computed(() => {
    const transactions = [...this.transactions()];
    const column = this.sortColumn();

    const direction =
      this.sortDirection() === 'asc' ? 1 : -1;

    return transactions.sort((first, second) => {
      if (column === 'transactionTime') {
        const firstTime =
          new Date(first.transactionTime).getTime();

        const secondTime =
          new Date(second.transactionTime).getTime();

        return (firstTime - secondTime) * direction;
      }

      const firstValue = first[column];
      const secondValue = second[column];

      if (
        typeof firstValue === 'number' &&
        typeof secondValue === 'number'
      ) {
        return (firstValue - secondValue) * direction;
      }

      return String(firstValue).localeCompare(
        String(secondValue),
        undefined,
        { sensitivity: 'base' }
      ) * direction;
    });
  });

  constructor(
    private transactionService: Transaction
  ) { }

  ngOnInit(): void {
    this.loadTransactions();
  }

  loadTransactions(): void {
    this.isLoading.set(true);
    this.loadError.set(null);

    this.transactionService.getTransactions().subscribe({
      next: (data) => {
        this.transactions.set(data);

        console.log('Transactions loaded:', data);

        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(
          'Error loading transactions:',
          err
        );

        this.loadError.set(
          'Unable to load transactions. ' +
          'Please check the API connection.'
        );

        this.isLoading.set(false);
      }
    });
  }

  importTransactions(): void {
    if (this.isImporting()) {
      return;
    }

    this.isImporting.set(true);
    this.errorMessage.set(null);
    this.importResult.set(null);

    this.transactionService
      .importTransactions()
      .subscribe({
        next: (result) => {
          console.log(
            'Import completed:',
            result
          );

          this.importResult.set(result);
          this.loadTransactions();
          this.isImporting.set(false);
        },
        error: (err) => {
          console.error(
            'Import failed:',
            err
          );

          this.errorMessage.set(
            'Transaction import failed. ' +
            'Please try again.'
          );

          this.isImporting.set(false);
        }
      });
  }

  sortBy(column: SortColumn): void {
    if (this.sortColumn() === column) {
      this.sortDirection.update(direction =>
        direction === 'asc' ? 'desc' : 'asc'
      );

      return;
    }

    this.sortColumn.set(column);
    this.sortDirection.set('asc');
  }

  sortIndicator(column: SortColumn): string {
    if (this.sortColumn() !== column) {
      return '';
    }

    return this.sortDirection() === 'asc'
      ? '▲'
      : '▼';
  }
}
