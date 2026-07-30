import { Check, X } from 'lucide-react'

interface Props {
  text: string
  variant: 'match' | 'gap'
}

export default function SkillChip({ text, variant }: Props) {
  const Icon = variant === 'match' ? Check : X

  return (
    <li className={`skill-chip skill-chip--${variant}`}>
      <Icon size={14} strokeWidth={2.5} aria-hidden="true" />
      <span>{text}</span>
    </li>
  )
}
