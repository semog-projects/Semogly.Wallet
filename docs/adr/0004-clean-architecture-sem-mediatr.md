# ADR 0004: Clean Architecture por serviço, com handlers próprios (sem MediatR)

**Status:** aceita

## Contexto

Cada microsserviço precisa de regras de negócio isoladas de frameworks (EF Core, MassTransit, Marten, Redis) para
que possam ser testadas e evoluídas sem acoplamento. O MediatR, comum nesse estilo, passou a ter licença comercial.

## Decisão

- **Quatro projetos por serviço**, com dependências sempre apontando para dentro:
  `Api → Infrastructure → Application → Domain`.
- **Handlers próprios:** interfaces `ICommandHandler`/`IQueryHandler` no SharedKernel, registradas na injeção de
  dependência e chamadas diretamente pelos endpoints e consumers. Não há mediator.
- **Result pattern:** falhas esperadas (não encontrado, conflito, validação) voltam como `Result` com `Error`
  tipado e são mapeadas para ProblemDetails (404/409/400). Violações de invariantes de domínio são
  `DomainException` (422).
- **CQRS leve:** a escrita passa por repositório e unit of work; a leitura passa por um *read service* que
  projeta direto para o DTO.
- **Consumers como adaptadores de entrada:** traduzem a mensagem em comando da Application.
- **Testes de arquitetura** validam a regra de dependência e que os handlers são `internal sealed`.

## Consequências

- **Positivas:**
  - Domínio e casos de uso testados com fakes, sem infraestrutura.
  - Fronteiras explícitas e verificadas no CI.
  - Nenhuma dependência comercial.
- **Negativas:**
  - Mais projetos e algum código repetido entre serviços (por exemplo, `IEventPublisher`), aceito em nome da
    autonomia de cada um.
