/** Telas planejadas nas próximas issues (#25 contas, #26 transferências). */
export function ComingSoonPage({ title, issue }: { title: string; issue: number }) {
  return (
    <section className="space-y-2">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-sm text-muted-foreground">Em construção (issue #{issue}).</p>
    </section>
  )
}
