import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CurrencyPipe } from '@angular/common';
import { TransactionModel } from '../models/transaction.model';
import { ImportResultModel } from '../models/import-result.model';

@Injectable({
  providedIn: 'root'
})
export class Transaction {

  private apiUrl = 'https://localhost:7089/api/transactions';

  constructor(private http: HttpClient) { }

  getTransactions(): Observable<TransactionModel[]> {
    return this.http.get<TransactionModel[]>(this.apiUrl);
  }

  importTransactions(): Observable<ImportResultModel> {
    return this.http.post<ImportResultModel>(
      'https://localhost:7089/api/import',
      {}
    );
  }
}
