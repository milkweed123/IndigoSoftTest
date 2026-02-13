
-- Step 1: Create new partitioned table
CREATE TABLE IF NOT EXISTS ticks_partitioned (
    id BIGSERIAL NOT NULL,
    instrument_id INTEGER NOT NULL,
    exchange VARCHAR(20) NOT NULL,
    symbol VARCHAR(50) NOT NULL,
    price NUMERIC(18,8) NOT NULL,
    volume NUMERIC(18,8) NOT NULL,
    timestamp TIMESTAMP NOT NULL,
    received_at TIMESTAMP NOT NULL,
    source_type VARCHAR(20) NOT NULL,
    CONSTRAINT fk_ticks_instrument FOREIGN KEY (instrument_id) REFERENCES instruments(id)
) PARTITION BY RANGE (timestamp);

-- Step 2: Create indexes on partitioned table
CREATE INDEX IF NOT EXISTS idx_ticks_partitioned_instrument_timestamp 
    ON ticks_partitioned (instrument_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_ticks_partitioned_timestamp 
    ON ticks_partitioned (timestamp DESC);

-- Step 3: Create partition management functions
CREATE OR REPLACE FUNCTION create_tick_partition(partition_date DATE)
RETURNS VOID AS $$
DECLARE
    partition_name TEXT;
    start_date TEXT;
    end_date TEXT;
BEGIN
    partition_name := 'ticks_' || TO_CHAR(partition_date, 'YYYY_MM_DD');
    start_date := partition_date::TEXT;
    end_date := (partition_date + INTERVAL '1 day')::TEXT;
    
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF ticks_partitioned
         FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION drop_old_tick_partitions(retention_days INTEGER DEFAULT 30)
RETURNS INTEGER AS $$
DECLARE
    partition_record RECORD;
    dropped_count INTEGER := 0;
    cutoff_date DATE;
BEGIN
    cutoff_date := CURRENT_DATE - retention_days;
    
    FOR partition_record IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE tablename LIKE 'ticks_20%'
          AND tablename < 'ticks_' || TO_CHAR(cutoff_date, 'YYYY_MM_DD')
    LOOP
        EXECUTE format('DROP TABLE IF EXISTS %I.%I', 
            partition_record.schemaname, partition_record.tablename);
        dropped_count := dropped_count + 1;
    END LOOP;
    
    RETURN dropped_count;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION ensure_future_tick_partitions(days_ahead INTEGER DEFAULT 7)
RETURNS INTEGER AS $$
DECLARE
    partition_date DATE;
    created_count INTEGER := 0;
BEGIN
    FOR i IN 1..days_ahead LOOP
        partition_date := CURRENT_DATE + i;
        
        IF NOT EXISTS (
            SELECT 1 FROM pg_tables 
            WHERE tablename = 'ticks_' || TO_CHAR(partition_date, 'YYYY_MM_DD')
        ) THEN
            PERFORM create_tick_partition(partition_date);
            created_count := created_count + 1;
        END IF;
    END LOOP;
    
    RETURN created_count;
END;
$$ LANGUAGE plpgsql;
