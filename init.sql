-- Схема базы данных PostgreSQL 18 для AlgorithmLab
CREATE TABLE IF NOT EXISTS experiments (
    id UUID PRIMARY KEY,
    algorithm_id VARCHAR(100) NOT NULL,
    algorithm_name VARCHAR(200) NOT NULL,
    category VARCHAR(100) NOT NULL,
    complexity_type VARCHAR(50) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    n_min INT NOT NULL,
    n_max INT NOT NULL,
    step INT NOT NULL,
    runs_per_n INT NOT NULL,
    c_factor DOUBLE PRECISION,
    mse DOUBLE PRECISION,
    rmse DOUBLE PRECISION,
    r_squared DOUBLE PRECISION,
    total_duration_ms DOUBLE PRECISION DEFAULT 0,
    config_hash VARCHAR(64)
);

CREATE TABLE IF NOT EXISTS experiment_points (
    id UUID PRIMARY KEY,
    experiment_id UUID REFERENCES experiments(id) ON DELETE CASCADE,
    n INT NOT NULL,
    m INT DEFAULT 0,
    avg_ms DOUBLE PRECISION NOT NULL,
    median_ms DOUBLE PRECISION NOT NULL,
    theo_ms DOUBLE PRECISION,
    step_count BIGINT DEFAULT 0,
    is_outlier BOOLEAN DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS point_runs (
    id UUID PRIMARY KEY,
    point_id UUID REFERENCES experiment_points(id) ON DELETE CASCADE,
    run_index INT NOT NULL,
    elapsed_ms DOUBLE PRECISION NOT NULL,
    is_outlier BOOLEAN DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS benchmark_cache (
    algorithm_id VARCHAR(100) NOT NULL,
    n INT NOT NULL,
    m INT DEFAULT 0,
    config_hash VARCHAR(64) NOT NULL,
    avg_ms DOUBLE PRECISION NOT NULL,
    median_ms DOUBLE PRECISION NOT NULL,
    step_count BIGINT DEFAULT 0,
    runs_json TEXT,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (algorithm_id, n, m, config_hash)
);

CREATE INDEX IF NOT EXISTS idx_experiment_algo ON experiments(algorithm_id);
CREATE INDEX IF NOT EXISTS idx_point_experiment ON experiment_points(experiment_id);
CREATE INDEX IF NOT EXISTS idx_point_n ON experiment_points(n);
CREATE INDEX IF NOT EXISTS idx_cache_lookup ON benchmark_cache(algorithm_id, config_hash);
