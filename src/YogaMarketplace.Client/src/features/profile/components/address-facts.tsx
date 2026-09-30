type AddressFact = {
  label: string;
  value: string;
};

export function AddressFacts({ facts }: { facts: AddressFact[] }) {
  return (
    <dl className="space-y-4 rounded-2xl bg-brand-background px-4 py-4">
      {facts.map((fact) => (
        <div key={fact.label} className="space-y-1">
          <dt className="text-[11px] font-medium tracking-[0.16em] text-brand-muted uppercase">{fact.label}</dt>
          <dd className="text-sm leading-relaxed text-brand-text">{fact.value}</dd>
        </div>
      ))}
    </dl>
  );
}
