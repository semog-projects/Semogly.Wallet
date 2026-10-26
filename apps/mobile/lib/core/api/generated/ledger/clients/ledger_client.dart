// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/deposit_request.dart';
import '../models/deposit_response.dart';
import '../models/ledger_book_response.dart';
import '../models/statement_response.dart';

part 'ledger_client.g.dart';

@RestApi()
abstract class LedgerClient {
  factory LedgerClient(Dio dio, {String? baseUrl}) = _LedgerClient;

  /// Visão do livro-razão: saldos de CashIn, Clearing e clientes (backoffice).
  ///
  /// No Gateway, exige o papel operator. Total diferente de zero indica inconsistência contábil.
  @GET('/ledger/book')
  Future<LedgerBookResponse> getBook();

  /// Registra um depósito (entrada de dinheiro externo) na conta
  @POST('/ledger/accounts/{accountId}/deposits')
  Future<DepositResponse> deposit({
    @Path('accountId') required String accountId,
    @Body() required DepositRequest body,
  });

  /// Consulta o extrato da conta, do lançamento mais recente para o mais antigo
  @GET('/ledger/accounts/{accountId}/statement')
  Future<StatementResponse> getStatement({
    @Path('accountId') required String accountId,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
  });
}
