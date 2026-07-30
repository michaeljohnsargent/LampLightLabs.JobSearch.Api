export type Theme = 'claude-dark' | 'claude-light' | 'professional' | 'bold' | 'minimal'

const THEMES: { id: Theme; label: string }[] = [
  { id: 'claude-dark',  label: 'Claude Dark' },
  { id: 'claude-light', label: 'Claude Light' },
  { id: 'professional', label: 'Professional' },
  { id: 'bold',         label: 'Bold' },
  { id: 'minimal',      label: 'Minimal' },
]

interface Props {
  theme: Theme
  onThemeChange: (theme: Theme) => void
}

export default function ThemeSwitcher({ theme, onThemeChange }: Props) {
  return (
    <div className="theme-switcher" role="group" aria-label="Select theme">
      {THEMES.map(t => (
        <button
          key={t.id}
          type="button"
          className={`theme-btn${t.id === theme ? ' theme-btn--active' : ''}`}
          onClick={() => onThemeChange(t.id)}
          aria-pressed={t.id === theme}
        >
          {t.label}
        </button>
      ))}
    </div>
  )
}
