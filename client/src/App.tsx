import { useEffect, useState, type FormEvent } from 'react'
import './App.css'
import ThemeSwitcher, { type Theme } from './ThemeSwitcher'
import ScoreGauge from './ScoreGauge'
import SkillChip from './SkillChip'
import Message from './Message'
import { UserContext } from './UserContext'
import { Sparkles, ArrowRight } from 'lucide-react'

const API_URL = 'https://lamplightlabs-api.azurewebsites.net/api/rag/match'
const MIN_WORDS = 10

interface RagMatchResponse {
  matchScore: number
  summary: string
  strengths: string[]
  gaps: string[]
  retrievedContext: string[]
}

function countWords(text: string): number {
  const trimmed = text.trim()
  return trimmed === '' ? 0 : trimmed.split(/\s+/).length
}

export default function App() {
  const [theme, setTheme] = useState<Theme>('claude-dark')
  const [jobDescription, setJobDescription] = useState('')
  const [result, setResult] = useState<RagMatchResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const applicant: string = 'Michael'

  useEffect(() => {
    document.documentElement.dataset.theme = theme
  }, [theme])

  const wordCount = countWords(jobDescription)
  const isReady = wordCount >= MIN_WORDS

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    setResult(null)

     try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ jobDescription }),
      })

      if (!res.ok) {
        const text = await res.text()
        throw new Error(`${res.status} ${res.statusText}${text ? ` — ${text}` : ''}`)
      }

      setResult(await res.json())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An unexpected error occurred.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <div className="top-glow" aria-hidden="true" />
      <main>
        <header>
          <div className="badge">
            <Sparkles size={14} aria-hidden="true" />
            <span>Resume Match Analyzer</span>
          </div>
          <h1>How well does your resume fit?</h1>
          <p className="subtitle">
            Paste a job description to see how well {applicant}'s resume matches.
          </p>
          <ThemeSwitcher theme={theme} onThemeChange={setTheme} />
          <UserContext.Provider value={{ name: applicant, message: 'Welcome to the Resume Match Analyzer!' }}>
            <Message />
          </UserContext.Provider>
        </header>

        <form onSubmit={handleSubmit}>
          <label htmlFor="jd">Job description</label>
          <textarea
            id="jd"
            className="jd-textarea"
            value={jobDescription}
            onChange={e => setJobDescription(e.target.value)}
            placeholder="Paste the full job description here…"
            required
            disabled={loading}
          />
          <div className="jd-meta">
            <span className="word-count">{wordCount} word{wordCount === 1 ? '' : 's'}</span>
            <span className={`readiness${isReady ? ' readiness--ready' : ''}`}>
              {isReady ? 'Ready to analyze' : 'At least 10 words needed'}
            </span>
          </div>
          <button type="submit" className="btn-primary" disabled={loading || !isReady}>
            {loading ? (
              'Analyzing…'
            ) : (
              <>
                Analyze match
                <ArrowRight size={16} className="btn-icon" aria-hidden="true" />
              </>
            )}
          </button>
        </form>

        {error && (
          <div className="error-box" role="alert">
            <strong>Error:</strong> {error}
          </div>
        )}

        {loading && (
          <div className="skeleton" aria-hidden="true">
            <div className="skeleton-circle" />
            <div className="skeleton-bar" />
            <div className="skeleton-cards">
              <div className="skeleton-card" />
              <div className="skeleton-card" />
            </div>
          </div>
        )}

        {result && !loading && (
          <section className="results">
            <div className="gauge-card">
              <ScoreGauge score={result.matchScore} />
              <p className="gauge-summary">{result.summary}</p>
            </div>

            <div className="result-columns">
              <div className="chip-card">
                <h2>Matched</h2>
                <ul className="chip-list">
                  {result.strengths.map((s, i) => (
                    <SkillChip key={i} text={s} variant="match" />
                  ))}
                </ul>
              </div>

              <div className="chip-card">
                <h2>Missing</h2>
                <ul className="chip-list">
                  {result.gaps.map((g, i) => (
                    <SkillChip key={i} text={g} variant="gap" />
                  ))}
                </ul>
              </div>
            </div>
          </section>
        )}
      </main>
    </>
  )
}
