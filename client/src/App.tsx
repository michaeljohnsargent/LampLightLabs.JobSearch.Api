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

const DEMO_STRONG_MATCH: RagMatchResponse = {
  matchScore: 92,
  summary:
    "Michael's resume shows strong, direct alignment with this role: production ASP.NET Core API experience, hands-on Azure deployment, and a shipped RAG/LLM integration nearly identical in shape to what this posting asks for. The main soft spot is container-orchestration depth.",
  strengths: [
    '5+ years building production C#/.NET Core APIs, including versioned REST endpoints and multiple auth schemes (JWT, OAuth2, API key)',
    'Hands-on Azure deployment experience: App Service, Static Web Apps, and CI/CD via GitHub Actions',
    'Shipped a live RAG/LLM integration end-to-end, from prompt design to a production endpoint',
    'EF Core and PostgreSQL experience in a real, deployed system, not just tutorials',
  ],
  gaps: [
    'Limited hands-on Kubernetes experience — deployments shown here are Azure App Service/PaaS, not container orchestration',
  ],
  retrievedContext: [],
}

const DEMO_PARTIAL_MATCH: RagMatchResponse = {
  matchScore: 58,
  summary:
    "This role's backend responsibilities — C#/.NET APIs, PostgreSQL, REST design — map cleanly onto Michael's experience. However, the posting expects the same engineer to own Angular development in production, and Michael's frontend work is a personal React project rather than professional Angular ownership.",
  strengths: [
    "Solid C#/.NET Core backend experience matching the role's core API and data-layer responsibilities",
    'Experience with EF Core/PostgreSQL and versioned REST API design',
    'Comfortable with Azure-hosted deployments, which aligns if this employer is also Azure-based',
  ],
  gaps: [
    "Role expects professional-level Angular ownership; Michael's frontend experience is a personal React project, not production Angular work",
    'No listed experience at the "full-stack owner" depth this posting implies for frontend responsibilities',
  ],
  retrievedContext: [],
}

const DEMO_WEAK_MATCH: RagMatchResponse = {
  matchScore: 24,
  summary:
    "This posting's core stack — Python, Django, and large-scale data-pipeline engineering — doesn't overlap with Michael's demonstrated experience, which is concentrated in C#/.NET. The only real connection is general familiarity with relational databases and cloud deployment concepts, not the Python ecosystem itself.",
  strengths: [
    'General relational database design experience (PostgreSQL) that would transfer conceptually to any backend stack',
    'Comfortable working with cloud-hosted APIs and CI/CD pipelines, regardless of language',
  ],
  gaps: [
    'No demonstrated Python or Django experience — the entire shown history is C#/.NET',
    'No experience with the large-scale data-pipeline tooling (e.g. Airflow, Spark) this role calls for',
    "This is a genuine stack mismatch, not a surface-level gap — the role's day-to-day work wouldn't draw on his primary skill set",
  ],
  retrievedContext: [],
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

  function handleDemo(fixture: RagMatchResponse) {
    setError(null)
    setLoading(false)
    setResult(fixture)
  }

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

        <div className="demo-section">
          <span className="demo-label">Try a sample result</span>
          <div className="demo-buttons">
            <button type="button" className="btn-ghost" onClick={() => handleDemo(DEMO_STRONG_MATCH)}>
              See a strong match
            </button>
            <button type="button" className="btn-ghost" onClick={() => handleDemo(DEMO_PARTIAL_MATCH)}>
              See a partial match
            </button>
            <button type="button" className="btn-ghost" onClick={() => handleDemo(DEMO_WEAK_MATCH)}>
              See a weak match
            </button>
          </div>
        </div>

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
