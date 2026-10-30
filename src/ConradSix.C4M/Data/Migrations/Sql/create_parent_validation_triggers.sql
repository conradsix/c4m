-- Triggers for parent validation on pages table

-- Trigger function that validates parent constraints
CREATE OR REPLACE FUNCTION cms.pages_parent_validation_trigger()
RETURNS TRIGGER AS $$
BEGIN
    -- Validate no cycles (only check on INSERT or when parent_id changes)
    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND OLD.parent_id IS DISTINCT FROM NEW.parent_id) THEN
        IF NOT cms.validate_no_cycles(NEW.id, NEW.parent_id) THEN
            RAISE EXCEPTION 'Cannot create circular reference in page hierarchy'
                USING ERRCODE = 'P0002',
                      DETAIL = 'Moving this page would create a circular reference in the page hierarchy',
                      HINT = 'Choose a different parent page that does not create a cycle';
        END IF;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create the trigger
CREATE TRIGGER pages_parent_validation_trigger
    BEFORE INSERT OR UPDATE ON cms.pages
    FOR EACH ROW
    EXECUTE FUNCTION cms.pages_parent_validation_trigger();
