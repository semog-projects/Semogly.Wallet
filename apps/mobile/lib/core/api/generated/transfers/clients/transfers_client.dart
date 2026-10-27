// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/create_transfer_request.dart';
import '../models/paged_response_of_transfer_response.dart';
import '../models/transfer_response.dart';
import '../models/transfer_status.dart';

part 'transfers_client.g.dart';

@RestApi()
abstract class TransfersClient {
  factory TransfersClient(Dio dio, {String? baseUrl}) = _TransfersClient;

  /// Solicita uma transferência entre contas.
  ///
  /// Processamento assíncrono via saga: retorna 202 e o status deve ser acompanhado em GET /transfers/{id}. Repetir a requisição com a mesma Idempotency-Key devolve a transferência original. A conta de origem precisa ser do usuário autenticado (ou ele ser operador).
  @POST('/transfers')
  Future<TransferResponse> createTransfer({
    @Body() required CreateTransferRequest body,
    @Header('Idempotency-Key') String? idempotencyKey,
  });

  /// Lista transferências com filtros (backoffice).
  ///
  /// Exige o papel operator. A conta filtra tanto pela origem quanto pelo destino.
  @GET('/transfers')
  Future<PagedResponseOfTransferResponse> listTransfers({
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('status') TransferStatus? status,
    @Query('accountId') String? accountId,
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
  });

  /// Consulta o status de uma transferência.
  ///
  /// Titular da conta de origem ou de destino, ou operador.
  @GET('/transfers/{id}')
  Future<TransferResponse> getTransfer({
    @Path('id') required String id,
  });
}
