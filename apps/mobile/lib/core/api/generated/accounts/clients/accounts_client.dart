// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/account_lookup_response.dart';
import '../models/account_response.dart';
import '../models/balance_response.dart';
import '../models/create_account_request.dart';
import '../models/paged_response_of_account_response.dart';

part 'accounts_client.g.dart';

@RestApi()
abstract class AccountsClient {
  factory AccountsClient(Dio dio, {String? baseUrl}) = _AccountsClient;

  /// Abre uma nova conta na carteira
  @POST('/accounts')
  Future<AccountResponse> createAccount({
    @Body() required CreateAccountRequest body,
  });

  /// Busca contas por nome, e-mail ou CPF (backoffice).
  ///
  /// No Gateway, exige o papel operator. Sem termo, lista as contas mais recentes.
  @GET('/accounts')
  Future<PagedResponseOfAccountResponse> searchAccounts({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('search') String? search,
  });

  /// Encontra a conta de destino pelo e-mail do titular (chave de transferência).
  ///
  /// Exige JWT. Retorna apenas o id e o nome mascarado, para o pagador confirmar o destinatário.
  @GET('/accounts/lookup')
  Future<AccountLookupResponse> lookupAccount({
    @Query('email') required String email,
  });

  /// Contas do titular autenticado (e-mail do token).
  ///
  /// Exige JWT: o próprio serviço valida o token repassado pelo Gateway.
  @GET('/accounts/me')
  Future<List<AccountResponse>> getMyAccounts();

  /// Consulta os dados de uma conta
  @GET('/accounts/{id}')
  Future<AccountResponse> getAccount({
    @Path('id') required String id,
  });

  /// Consulta o saldo de uma conta
  @GET('/accounts/{id}/balance')
  Future<BalanceResponse> getBalance({
    @Path('id') required String id,
  });
}
