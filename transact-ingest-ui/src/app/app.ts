import { Component } from '@angular/core';
import { TransactionList } from './features/transactions/transaction-list/transaction-list';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [TransactionList],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
}
