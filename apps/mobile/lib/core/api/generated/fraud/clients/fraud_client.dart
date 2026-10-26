// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/daily_usage_response.dart';

part 'fraud_client.g.dart';

@RestApi()
abstract class FraudClient {
  factory FraudClient(Dio dio, {String? baseUrl}) = _FraudClient;

  /// Consulta quanto do limite diário de transferências a conta já usou hoje
  @GET('/fraud/accounts/{accountId}/daily-usage')
  Future<DailyUsageResponse> getDailyUsage({
    @Path('accountId') required String accountId,
  });
}
